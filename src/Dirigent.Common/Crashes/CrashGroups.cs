using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Crashes
{
	/// <summary>The crashes of one bug: the same program, and the same place in it.</summary>
	public class CrashGroup
	{
		public string key { get; set; } = string.Empty;
		public string kind { get; set; } = string.Empty;
		public string exe { get; set; } = string.Empty;

		// the key parts: a native crash has the first three, a managed one the last two
		public string? faultingModule { get; set; }
		public string? exceptionCode { get; set; }
		public string? faultingOffset { get; set; }
		public string? exceptionType { get; set; }
		public string? topFrame { get; set; }

		public int count { get; set; }

		/// <summary>UTC.</summary>
		public DateTime first { get; set; }
		public DateTime last { get; set; }

		public List<string> machines { get; set; } = new();
		public List<string> apps { get; set; } = new();
		public List<string> exeVersions { get; set; } = new();

		/// <summary>The id of every crash of the group, newest first: each can be listed (<c>?id=</c>), collected and inspected alone.</summary>
		public List<string> ids { get; set; } = new();
	}

	/// <summary>The answer of <c>GET /api/crashes?groupBy=signature</c>.</summary>
	public class CrashGroupedListing
	{
		/// <summary>By count, then by the time of the last crash, both descending.</summary>
		public List<CrashGroup> groups { get; set; } = new();

		/// <summary>The crashes that are not grouped - their identity is not reliable, or they are of a kind that is never grouped - in full, newest first.</summary>
		public List<CrashEntry> ungrouped { get; set; } = new();
		public List<CrashMachineStatus> machines { get; set; } = new();
		public CrashTotals totals { get; set; } = new();
	}

	/// <summary>
	/// Grouping the crashes of one bug. Better one line too many than two different bugs in one: a crash is grouped only when every part of its
	/// identity is known and is not a placeholder; any other is left alone.
	/// </summary>
	public static class CrashGroups
	{
		public const string BySignature = "signature";

		/// <summary>Whether a value of <c>groupBy</c> is known (empty: none).</summary>
		/// <exception cref="ArgumentException">an unknown value</exception>
		public static bool IsGrouping( string? groupBy )
		{
			if( string.IsNullOrWhiteSpace( groupBy ) ) return false;
			if( string.Equals( groupBy.Trim(), BySignature, StringComparison.OrdinalIgnoreCase ) ) return true;
			throw new ArgumentException( $"'groupBy' can only be '{BySignature}', not '{groupBy}'." );
		}

		/// <summary>The parts of the key of a crash, or null when it is not to be grouped.</summary>
		public class Key
		{
			public string Text = string.Empty;
			public string Exe = string.Empty;
			public string? Module, Code, Offset, ExceptionType, TopFrame;
		}

		static string? ExeName( string? exe )
		{
			if( string.IsNullOrWhiteSpace( exe ) ) return null;
			var name = exe.Trim().Substring( exe.Trim().LastIndexOfAny( new[] { (char) 92, '/' } ) + 1 );
			return name.Length == 0 || name.Equals( "unknown", StringComparison.OrdinalIgnoreCase ) ? null : name;
		}

		static bool IsPlaceholderModule( string? module )
			=> string.IsNullOrWhiteSpace( module ) || module.Trim().Equals( "unknown", StringComparison.OrdinalIgnoreCase )
				|| module.Trim().StartsWith( "StackHash", StringComparison.OrdinalIgnoreCase );

		static bool IsZeroOrMissing( string? offset )
		{
			var trimmed = CrashBuilder.TrimmedHex( offset );
			return trimmed is null || trimmed == "0x0" || !trimmed.StartsWith( "0x" );
		}

		/// <summary>The exception type of a managed crash: the first line of its text up to the colon.</summary>
		public static string? ExceptionTypeOf( string? exceptionText )
		{
			if( string.IsNullOrWhiteSpace( exceptionText ) ) return null;
			var first = exceptionText.Replace( "\r\n", "\n" ).Split( '\n' )[0].Trim();
			var type = first.Split( ':' )[0].Trim();
			return type.Length == 0 || type.StartsWith( "at ", StringComparison.Ordinal ) ? null : type;
		}

		/// <summary>The first "at ..." line of the text, without its arguments, file and line: <c>at A.B(String x) in f.cs:line 3</c> is <c>A.B</c>.</summary>
		public static string? TopFrameOf( string? exceptionText )
		{
			if( string.IsNullOrWhiteSpace( exceptionText ) ) return null;

			foreach( var raw in exceptionText.Replace( "\r\n", "\n" ).Split( '\n' ) )
			{
				var line = raw.Trim();
				if( !line.StartsWith( "at ", StringComparison.Ordinal ) ) continue;

				var frame = line.Substring( 3 ).Trim();
				var paren = frame.IndexOf( '(' );
				if( paren >= 0 ) frame = frame.Substring( 0, paren );
				var inFile = frame.IndexOf( " in ", StringComparison.Ordinal );
				if( inFile >= 0 ) frame = frame.Substring( 0, inFile );
				frame = frame.Trim();
				return frame.Length == 0 ? null : frame;
			}
			return null;
		}

		/// <summary>
		/// The key of the crash, or null: the entry is not grouped. A <c>crash</c> is the exe, the faulting module, the exception code and the offset in the module;
		/// a <c>managed</c> one the exe, the exception type and the top frame; a <c>hang</c> the exe. Anything else is never grouped. A crash whose key lacks a part
		/// or has a placeholder for one - an unknown module, a StackHash, an offset of 0, no exception code, no 1026 text - is left alone.
		/// </summary>
		public static Key? KeyOf( CrashEntry e )
		{
			var exe = ExeName( e.exe );
			if( exe is null ) return null;

			switch( e.kind )
			{
				case CrashKind.Crash:
				{
					if( IsPlaceholderModule( e.faultingModule ) ) return null;
					if( string.IsNullOrWhiteSpace( e.exceptionCode ) ) return null;
					if( IsZeroOrMissing( e.faultingOffset ) ) return null;

					var code = CrashBuilder.NormalizeCode( e.exceptionCode )!;
					var offset = CrashBuilder.TrimmedHex( e.faultingOffset )!;
					var module = e.faultingModule!.Trim();
					return new Key { Text = $"crash|{exe.ToLowerInvariant()}|{module.ToLowerInvariant()}|{code}|{offset}", Exe = exe, Module = module, Code = code, Offset = offset };
				}

				case CrashKind.Managed:
				{
					var type = ExceptionTypeOf( e.exceptionText );
					var frame = TopFrameOf( e.exceptionText );
					if( type is null || frame is null ) return null;

					return new Key { Text = $"managed|{exe.ToLowerInvariant()}|{type}|{frame}", Exe = exe, ExceptionType = type, TopFrame = frame };
				}

				case CrashKind.Hang:
					return new Key { Text = $"hang|{exe.ToLowerInvariant()}", Exe = exe };

				default:
					return null; // exit, reboot, bugcheck, gpuReset
			}
		}

		/// <summary>The groups of the entries and what is left alone.</summary>
		public static (List<CrashGroup> groups, List<CrashEntry> ungrouped) Group( IEnumerable<CrashEntry> entries )
		{
			var ungrouped = new List<CrashEntry>();
			var byKey = new Dictionary<string, (Key key, List<CrashEntry> list)>( StringComparer.Ordinal );

			foreach( var e in entries.OrderByDescending( x => x.time ).ThenBy( x => x.id, StringComparer.Ordinal ) )
			{
				var key = KeyOf( e );
				if( key is null ) { ungrouped.Add( e ); continue; }

				if( !byKey.TryGetValue( key.Text, out var g ) ) byKey[key.Text] = g = (key, new List<CrashEntry>());
				g.list.Add( e );
			}

			var groups = byKey.Values.Select( g =>
			{
				var list = g.list; // newest first
				return new CrashGroup
				{
					key = g.key.Text,
					kind = list[0].kind,
					exe = g.key.Exe,
					faultingModule = g.key.Module,
					exceptionCode = g.key.Code,
					faultingOffset = g.key.Offset,
					exceptionType = g.key.ExceptionType,
					topFrame = g.key.TopFrame,
					count = list.Count,
					first = list.Min( x => x.time ),
					last = list.Max( x => x.time ),
					machines = list.Select( x => x.machine ).Distinct( StringComparer.OrdinalIgnoreCase ).OrderBy( x => x, StringComparer.OrdinalIgnoreCase ).ToList(),
					apps = list.SelectMany( x => x.app is not null ? new List<string> { x.app } : x.apps ).Distinct( StringComparer.OrdinalIgnoreCase ).OrderBy( x => x, StringComparer.OrdinalIgnoreCase ).ToList(),
					exeVersions = list.Select( x => x.exeVersion ).Where( x => !string.IsNullOrEmpty( x ) ).Select( x => x! ).Distinct( StringComparer.Ordinal ).OrderBy( x => x, StringComparer.Ordinal ).ToList(),
					ids = list.Select( x => x.id ).ToList(),
				};
			} ).OrderByDescending( g => g.count ).ThenByDescending( g => g.last ).ThenBy( g => g.key, StringComparer.Ordinal ).ToList();

			return (groups, ungrouped);
		}

		/// <summary>The grouped answer of a listing: its entries grouped, its machines and totals as they were.</summary>
		public static CrashGroupedListing Of( CrashListing listing )
		{
			var (groups, ungrouped) = Group( listing.crashes );
			return new CrashGroupedListing { groups = groups, ungrouped = ungrouped, machines = listing.machines, totals = listing.totals };
		}
	}
}
