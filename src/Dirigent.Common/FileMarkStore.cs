using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dirigent
{
	/// <summary>
	/// Remembers, per file on this machine, how far it had been written when somebody drew a line
	/// under it - so that a later collection can take only what came after.
	/// </summary>
	/// <remarks>
	/// A mark rather than an empty file, because on a running system the file usually cannot be
	/// emptied: a logger holding it open refuses both deletion and truncation, and where truncation
	/// does succeed the logger keeps its own offset and the file comes back as a run of NUL bytes.
	/// Reading a length always works and changes nothing, which is also why marking is safe to offer
	/// on a production site where the history must survive.
	///
	/// Lives on the machine that owns the files, because that is where the collecting script runs.
	/// Keyed by path rather than by node or package, so that "mark, then collect" holds however the
	/// collection happens to be assembled.
	/// </remarks>
	public class FileMarkStore
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		/// <summary>
		/// Where a file had got to, and enough about its identity to notice when it is not the same
		/// file any more.
		/// </summary>
		public class Mark
		{
			/// <summary>Length of the file when it was marked - where a collection starts.</summary>
			public long Offset;

			/// <summary>
			/// Creation time of the marked file. A different one means the file was replaced, which
			/// is what rotation looks like from here.
			/// </summary>
			public DateTime CreatedUtc;

			/// <summary>When the mark was made, for the archive to say which run it delimits.</summary>
			public DateTime MarkedAt;

			/// <summary>
			/// What put it there - "Clear" or "Mark". Empty in a store written before this was kept.
			/// </summary>
			/// <remarks>
			/// Worth a word in the archive: somebody who ran a Clear does not expect to read about a
			/// "mark" afterwards, and wonders whether the Clear did anything. It did - a log that is
			/// being written to cannot be emptied, so a Clear draws the line instead, which is how it
			/// keeps its promise that the next collection holds only what came after it.
			/// </remarks>
			public string MadeBy = string.Empty;

			/// <summary>
			/// The last few bytes before the mark, as they were when it was made. Null if the file was
			/// empty, or could not be read at that moment.
			/// </summary>
			/// <remarks>
			/// This is what actually tells the marked file from a different one wearing its name, and
			/// it is here because the obvious identity check does not hold on Windows: NTFS *tunneling*
			/// puts the original creation time back on a file deleted and recreated under the same name
			/// within about fifteen seconds - which is exactly what a rotating logger does. A rotated
			/// file therefore arrives with the marked file's creation time, and starting at the mark
			/// would deliver a slice of the middle of an unrelated file as if it were the test run.
			///
			/// Comparing the bytes just before the offset checks the one thing that has to be true for
			/// the offset to mean anything: that the boundary is still where it was put.
			/// </remarks>
			public byte[]? Fingerprint;
		}

		/// <summary>How many bytes before the mark are kept to recognise the file by.</summary>
		const int _fingerprintBytes = 32;

		class Content
		{
			// path -> mark; the comparer is applied on load, so the file itself needs no ordering
			public Dictionary<string, Mark> Marks = new();
		}

		readonly string _filePath;
		readonly object _lock = new();

		Dictionary<string, Mark> _marks = new( StringComparer.OrdinalIgnoreCase );

		/// <param name="folder">
		/// Where to keep the file. Empty selects the same default as the agent status file,
		/// %LocalAppData%\Dirigent, so that a test bed pointing the one somewhere isolated moves the
		/// other with it.
		/// </param>
		public FileMarkStore( string machineId, string? folder = null )
		{
			_filePath = GetFilePath( machineId, folder );
			Load();
		}

		public static string GetFilePath( string machineId, string? folder = null )
		{
			if( string.IsNullOrEmpty( folder ) )
			{
				var localAppData = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );
				folder = Path.Combine( localAppData, "Dirigent" );
			}

			return Path.Combine( folder, $"file_marks_{machineId}.json" );
		}

		/// <summary>The mark of a file, or null if nobody has drawn a line under it.</summary>
		public Mark? Get( string path )
		{
			lock( _lock )
			{
				return _marks.TryGetValue( path, out var mark ) ? mark : null;
			}
		}

		/// <summary>
		/// Marks the file at its current length. Returns the mark, or null if the file could not be
		/// looked at - which the caller reports rather than swallows.
		/// </summary>
		/// <param name="madeBy">the operation drawing the line - "Clear" or "Mark"</param>
		public Mark? MarkFile( string path, string madeBy = "Mark" )
		{
			try
			{
				var info = new FileInfo( path );
				if( !info.Exists ) return null;

				var mark = new Mark()
				{
					Offset = info.Length,
					CreatedUtc = info.CreationTimeUtc,
					MarkedAt = DateTime.Now,
					MadeBy = madeBy,
					Fingerprint = ReadFingerprint( path, info.Length ),
				};

				lock( _lock ) _marks[path] = mark;
				return mark;
			}
			catch( Exception e )
			{
				log.Warn( $"Could not mark '{path}': {e.Message}" );
				return null;
			}
		}

		/// <summary>
		/// The operation a mark came from, as a word for a person to read.
		/// </summary>
		public static string OperationOf( Mark mark )
			=> string.IsNullOrEmpty( mark.MadeBy ) ? "mark" : mark.MadeBy;

		/// <summary>Forgets the mark of a file. True if there was one.</summary>
		public bool Unmark( string path )
		{
			lock( _lock ) return _marks.Remove( path );
		}

		/// <summary>
		/// The last few bytes ending at the given offset, or null if they cannot be had.
		/// </summary>
		/// <remarks>
		/// Read with the share flags a live log needs: the file is almost certainly held open for
		/// writing by the application producing it, and an open that does not permit that is refused.
		/// A failure here is not worth reporting - the mark still has its length and creation time,
		/// which catch everything but a tunneled replacement.
		/// </remarks>
		static byte[]? ReadFingerprint( string path, long offset )
		{
			if( offset <= 0 ) return null;

			var count = (int) Math.Min( _fingerprintBytes, offset );

			try
			{
				using var file = new FileStream( path, FileMode.Open, FileAccess.Read,
												FileShare.ReadWrite | FileShare.Delete );
				file.Position = offset - count;

				var buffer = new byte[count];
				file.ReadExactly( buffer, 0, count );
				return buffer;
			}
			catch( Exception e )
			{
				log.Debug( $"Could not read the mark fingerprint of '{path}': {e.Message}" );
				return null;
			}
		}

		/// <summary>
		/// Whether the bytes before the mark are still the ones that were there when it was made.
		/// </summary>
		static bool FingerprintMatches( string path, Mark mark )
		{
			// nothing was recorded - an empty file, or one that could not be read at the time
			if( mark.Fingerprint is null || mark.Fingerprint.Length == 0 ) return true;

			var now = ReadFingerprint( path, mark.Offset );
			if( now is null ) return true; // cannot tell; the other checks have to do

			return now.AsSpan().SequenceEqual( mark.Fingerprint );
		}

		void Load()
		{
			try
			{
				if( !File.Exists( _filePath ) ) return;

				var content = Tools.Deserialize<Content>( File.ReadAllText( _filePath ) );
				if( content?.Marks is null ) return;

				lock( _lock )
				{
					_marks = new Dictionary<string, Mark>( content.Marks, StringComparer.OrdinalIgnoreCase );
				}
			}
			catch( Exception e )
			{
				// a damaged store is not worth failing a collection over; it means no marks, which is
				// the same as never having marked anything
				log.Warn( $"Could not read the file marks from '{_filePath}': {e.Message}" );
			}
		}

		/// <summary>
		/// Writes the marks out. Through a temporary name, so that a store found on disk is always a
		/// whole one - it is read by a different script than the one that wrote it.
		/// </summary>
		public void Save()
		{
			try
			{
				var folder = Path.GetDirectoryName( _filePath );
				if( !string.IsNullOrEmpty( folder ) ) Directory.CreateDirectory( folder );

				DropMarksOfFilesThatAreGone();

				Content content;
				lock( _lock ) content = new Content() { Marks = new Dictionary<string, Mark>( _marks ) };

				var temp = _filePath + ".writing";
				File.WriteAllText( temp, Tools.Serialize( content ) );
				File.Move( temp, _filePath, true );
			}
			catch( Exception e )
			{
				log.Error( $"Could not write the file marks to '{_filePath}': {e.Message}" );
			}
		}

		/// <summary>
		/// Forgets the marks of files that are no longer there.
		/// </summary>
		/// <remarks>
		/// A line drawn under a file that has since vanished says nothing true about whatever may
		/// appear under that name later, so it is only a trap - and one nothing else would clear
		/// away: a rotated-out log leaves the resolution of its node altogether, so a later Clear or
		/// Mark never visits it and never has the chance to drop its mark by hand.
		///
		/// A file that is only briefly absent - caught mid-rotation - loses its mark too, and that is
		/// the right way round: the next collection then takes it whole rather than cutting it at an
		/// offset that may no longer mean anything.
		///
		/// Done when the marks are written, which is when somebody is redrawing the lines anyway.
		/// </remarks>
		void DropMarksOfFilesThatAreGone()
		{
			List<string> gone;

			lock( _lock )
			{
				gone = _marks.Keys.Where( path => !Exists( path ) ).ToList();
				foreach( var path in gone ) _marks.Remove( path );
			}

			if( gone.Count > 0 )
				log.Debug( $"Forgot the marks of {gone.Count} file(s) that are no longer there." );
		}

		/// <summary>
		/// Whether the file is there. A path we cannot even look at counts as present - being unable
		/// to see a file is not evidence that it is gone, and forgetting a mark on that basis would
		/// quietly widen the next collection.
		/// </summary>
		static bool Exists( string path )
		{
			try { return File.Exists( path ); }
			catch { return true; }
		}

		/// <summary>
		/// Where a collection of this file should start, and why - given its mark, if any.
		/// </summary>
		/// <remarks>
		/// A mark that no longer describes the file on disk is worth more as a warning than as an
		/// offset: the file was rotated or truncated, so the run's lines are somewhere in what is
		/// there now. Collecting the whole file gives slightly more than the run rather than nothing
		/// of it, which is the right direction to be wrong in.
		/// </remarks>
		public (long Offset, string? Note) WhereToStart( string path, long currentLength, DateTime currentCreatedUtc )
		{
			var mark = Get( path );
			if( mark is null ) return ( 0, null );

			// named for what the operator actually did, not for the mechanism underneath
			var drawn = $"{OperationOf( mark )} of {mark.MarkedAt:yyyy-MM-dd HH:mm:ss}";

			if( mark.CreatedUtc != currentCreatedUtc )
				return ( 0, $"the whole file: it was replaced since the {drawn}" );

			if( currentLength < mark.Offset )
				return ( 0, $"the whole file: it was truncated or rotated since the {drawn}" );

			// same name, same creation time, long enough - and still a different file; see
			// Mark.Fingerprint for how Windows manages that
			if( !FingerprintMatches( path, mark ) )
				return ( 0, $"the whole file: it was replaced since the {drawn}" );

			// a file that was empty when it was marked is collected whole, and there is nothing to
			// explain about that - saying "from byte 0" would only puzzle the reader
			if( mark.Offset <= 0 ) return ( 0, null );

			return ( mark.Offset, $"written after the {drawn} (byte {mark.Offset})" );
		}
	}
}
