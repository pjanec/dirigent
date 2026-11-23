using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Dirigent.Crashes
{
	/// <summary>The part of a Windows event record the crash reading needs; no XML kept.</summary>
	public class RawEvent
	{
		public string Log = "Application";
		public string Provider = string.Empty;
		public int Id;
		public DateTime TimeUtc;

		/// <summary>1 critical, 2 error, 3 warning, 4 information.</summary>
		public int Level = 4;

		/// <summary>The values of the event's Data elements, in order.</summary>
		public List<string> Data = new();

		/// <summary>The Data elements that have names (Kernel-Power's), by name.</summary>
		public Dictionary<string, string> Named = new( StringComparer.OrdinalIgnoreCase );

		public string D( int i ) => i >= 0 && i < Data.Count ? Data[i] : string.Empty;
	}

	/// <summary>What to read: events of the listed providers/ids, written between the times.</summary>
	public class CrashEventQuery
	{
		public string Log = "Application";
		public DateTime FromUtc;
		public DateTime ToUtc;

		/// <summary>(provider name, event id); a provider matches by its name.</summary>
		public List<(string provider, int id)> Wanted = new();

		/// <summary>When set, every provider and id counts, but only events of this level or more severe (1 critical .. 4 information).</summary>
		public int? MaxLevel;

		/// <summary>No more than this many events are read - the newest first.</summary>
		public int MaxEvents = 5000;

		/// <summary>The time the reading may take.</summary>
		public TimeSpan TimeLimit = TimeSpan.FromSeconds( 20 );
	}

	public class CrashEventResult
	{
		public List<RawEvent> Events = new();

		/// <summary>Reading stopped at the limit of events or of time.</summary>
		public bool Cut;
	}

	/// <summary>Where the events come from: the Windows event log, or a test's list.</summary>
	public interface ICrashEventSource
	{
		CrashEventResult Read( CrashEventQuery query );

		/// <summary>The time of the oldest record of the log - how far back it reaches; null if it is empty or unreadable.</summary>
		DateTime? OldestEvent( string log );
	}

	/// <summary>The XML of an event record to <see cref="RawEvent"/>, and the query to the XPath of the log reader. Pure.</summary>
	public static class CrashEventXml
	{
		public static RawEvent Parse( string xml, string log = "Application" )
		{
			var doc = XDocument.Parse( xml );
			var root = doc.Root ?? throw new FormatException( "No event." );

			XElement? Child( XElement parent, string name ) => parent.Elements().FirstOrDefault( x => x.Name.LocalName == name );

			var system = Child( root, "System" ) ?? throw new FormatException( "No System element." );
			var ev = new RawEvent { Log = log };
			ev.Provider = Child( system, "Provider" )?.Attribute( "Name" )?.Value ?? string.Empty;
			ev.Id = int.Parse( Child( system, "EventID" )?.Value ?? "0", CultureInfo.InvariantCulture );

			if( int.TryParse( Child( system, "Level" )?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level ) ) ev.Level = level;

			var created = Child( system, "TimeCreated" )?.Attribute( "SystemTime" )?.Value;
			if( created is not null )
				ev.TimeUtc = DateTime.Parse( created, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal );

			var data = Child( root, "EventData" ) ?? Child( root, "UserData" );
			if( data is not null )
			{
				foreach( var d in data.Descendants().Where( x => x.Name.LocalName == "Data" ) )
				{
					ev.Data.Add( d.Value );
					var name = d.Attribute( "Name" )?.Value;
					if( !string.IsNullOrEmpty( name ) ) ev.Named[name] = d.Value;
				}
			}

			return ev;
		}

		/// <summary>The XPath of the Event Log API: the providers and ids asked for, within the times.</summary>
		public static string BuildXPath( CrashEventQuery q )
		{
			var wanted = q.MaxLevel is int level
				? $"Level>=1 and Level<={level}"
				: string.Join( " or ", q.Wanted.Select( w =>
					$"(Provider[@Name='{w.provider.Replace( "'", "" )}'] and EventID={w.id})" ) );

			string T( DateTime t ) => t.ToUniversalTime().ToString( "yyyy-MM-ddTHH:mm:ss.fff'Z'", CultureInfo.InvariantCulture );

			return $"*[System[({wanted}) and TimeCreated[@SystemTime>='{T( q.FromUtc )}' and @SystemTime<='{T( q.ToUtc )}']]]";
		}
	}

#if Windows
	/// <summary>The real thing: the Windows event log of this machine.</summary>
	public class WindowsCrashEventSource : ICrashEventSource
	{
		public CrashEventResult Read( CrashEventQuery q )
		{
			var res = new CrashEventResult();
			var started = DateTime.UtcNow;

			var query = new System.Diagnostics.Eventing.Reader.EventLogQuery( q.Log, System.Diagnostics.Eventing.Reader.PathType.LogName, CrashEventXml.BuildXPath( q ) )
			{
				ReverseDirection = true, // the newest first, so that a cut loses the oldest
			};

			using var reader = new System.Diagnostics.Eventing.Reader.EventLogReader( query );
			while( true )
			{
				if( res.Events.Count >= q.MaxEvents || DateTime.UtcNow - started > q.TimeLimit )
				{
					res.Cut = true;
					break;
				}

				using var record = reader.ReadEvent();
				if( record is null ) break;

				try
				{
					res.Events.Add( CrashEventXml.Parse( record.ToXml(), q.Log ) );
				}
				catch( Exception )
				{
					// an event we cannot read is not a crash we can list
				}
			}

			return res;
		}

		public DateTime? OldestEvent( string log )
		{
			try
			{
				var query = new System.Diagnostics.Eventing.Reader.EventLogQuery( log, System.Diagnostics.Eventing.Reader.PathType.LogName, "*" )
				{
					ReverseDirection = false,
				};
				using var reader = new System.Diagnostics.Eventing.Reader.EventLogReader( query );
				using var record = reader.ReadEvent();
				return record?.TimeCreated?.ToUniversalTime();
			}
			catch( Exception )
			{
				return null;
			}
		}
	}
#endif
}
