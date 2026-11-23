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

		/// <summary>
		/// A record of how long a file was at a moment - made by every mark, and by the checkpoints a running plan takes
		/// (its MarkInterval). Never moves the mark: only a collection "since a time" reads these.
		/// </summary>
		public class Point
		{
			public long Offset;
			public DateTime CreatedUtc;
			public DateTime AtUtc;

			/// <summary>"Mark", "Clear" or "Checkpoint".</summary>
			public string MadeBy = string.Empty;

			public byte[]? Fingerprint;
		}

		/// <summary>
		/// How long the points are kept, and how many per file at most - the newest win.
		/// Testers report bugs weeks later, so it is a month; the older the points, the thinner - see <see cref="Thin"/>.
		/// </summary>
		public static TimeSpan HistoryKeptFor = TimeSpan.FromDays( 31 );
		public const int MaxPointsPerFile = 2000;

		/// <summary>Every point is kept this long; after that the checkpoints get thinned to one per hour.</summary>
		public static TimeSpan HistoryFullFor = TimeSpan.FromDays( 2 );

		/// <summary>After this long the checkpoints get thinned further, to one per six hours.</summary>
		public static TimeSpan HistoryHourlyFor = TimeSpan.FromDays( 7 );

		class Content
		{
			// path -> mark; the comparer is applied on load, so the file itself needs no ordering
			public Dictionary<string, Mark> Marks = new();

			// path -> points, oldest first; absent in a store written before there were any
			public Dictionary<string, List<Point>>? History;
		}

		readonly string _filePath;
		readonly object _lock = new();
		DateTime _loadedWriteUtc;

		Dictionary<string, Mark> _marks = new( StringComparer.OrdinalIgnoreCase );
		Dictionary<string, List<Point>> _history = new( StringComparer.OrdinalIgnoreCase );

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

		/// <summary>
		/// Reads the store again if the file was written since it was read. For whoever only looks at the marks
		/// of an agent - the master, showing them in a preview - and so has to follow what the agent writes.
		/// </summary>
		public void ReloadIfChanged()
		{
			try
			{
				var write = File.Exists( _filePath ) ? File.GetLastWriteTimeUtc( _filePath ) : default;
				if( write != _loadedWriteUtc )
				{
					lock( _lock ) { _marks = new( StringComparer.OrdinalIgnoreCase ); _history = new( StringComparer.OrdinalIgnoreCase ); }
					Load();
					_loadedWriteUtc = write;
				}
			}
			catch( Exception e )
			{
				log.Debug( $"Could not follow the file marks in '{_filePath}': {e.Message}" );
			}
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

				lock( _lock )
				{
					_marks[path] = mark;
					// a mark is a moment too: "since" a time can start from it
					AddPoint( path, new Point() { Offset = mark.Offset, CreatedUtc = mark.CreatedUtc, AtUtc = DateTime.UtcNow,
												  MadeBy = madeBy, Fingerprint = mark.Fingerprint } );
				}
				return mark;
			}
			catch( Exception e )
			{
				log.Warn( $"Could not mark '{path}': {e.Message}" );
				return null;
			}
		}

		/// <summary>
		/// Records how long the file is now, in its history only - the mark, if any, stays where it is. Null if the
		/// file could not be looked at.
		/// </summary>
		public Point? Checkpoint( string path )
		{
			try
			{
				var info = new FileInfo( path );
				if( !info.Exists ) return null;

				var point = new Point()
				{
					Offset = info.Length,
					CreatedUtc = info.CreationTimeUtc,
					AtUtc = DateTime.UtcNow,
					MadeBy = "Checkpoint",
					Fingerprint = ReadFingerprint( path, info.Length ),
				};

				lock( _lock ) AddPoint( path, point );
				return point;
			}
			catch( Exception e )
			{
				log.Warn( $"Could not take a checkpoint of '{path}': {e.Message}" );
				return null;
			}
		}

		/// <summary>The recorded points of a file, oldest first.</summary>
		public IReadOnlyList<Point> History( string path )
		{
			lock( _lock ) return _history.TryGetValue( path, out var h ) ? h.ToList() : new List<Point>();
		}

		// under _lock
		void AddPoint( string path, Point point )
		{
			if( !_history.TryGetValue( path, out var h ) ) _history[path] = h = new List<Point>();
			h.Add( point );
			Trim( h );
		}

		static void Trim( List<Point> h ) => Thin( h, DateTime.UtcNow );

		/// <summary>
		/// Drops what is too old and thins the rest of a file's points (oldest first), as of the given time:
		/// every point is kept while younger than <see cref="HistoryFullFor"/>; older than that, at most one checkpoint
		/// per hour; older than <see cref="HistoryHourlyFor"/>, at most one per six hours. Marks and clears are never
		/// thinned - they are what the operator drew - only dropped with the rest after <see cref="HistoryKeptFor"/>.
		/// <see cref="MaxPointsPerFile"/> stays the hard cap.
		/// </summary>
		public static void Thin( List<Point> h, DateTime nowUtc )
		{
			var oldest = nowUtc - HistoryKeptFor;
			h.RemoveAll( p => p.AtUtc < oldest );

			var seen = new HashSet<long>();
			h.RemoveAll( p =>
			{
				if( p.MadeBy != "Checkpoint" ) return false;

				var age = nowUtc - p.AtUtc;
				if( age < HistoryFullFor ) return false;

				var bucket = age < HistoryHourlyFor ? TimeSpan.FromHours( 1 ) : TimeSpan.FromHours( 6 );

				// the first checkpoint of a bucket stays; the bucket id also says which kind of bucket it is,
				// so a point does not collide with one of the other width
				var id = ( p.AtUtc.Ticks / bucket.Ticks ) * 2 + ( bucket.Ticks == TimeSpan.TicksPerHour ? 0 : 1 );
				return !seen.Add( id );
			} );

			if( h.Count > MaxPointsPerFile ) h.RemoveRange( 0, h.Count - MaxPointsPerFile );
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

				_loadedWriteUtc = File.GetLastWriteTimeUtc( _filePath );
				var content = Tools.Deserialize<Content>( File.ReadAllText( _filePath ) );
				if( content?.Marks is null ) return;

				lock( _lock )
				{
					_marks = new Dictionary<string, Mark>( content.Marks, StringComparer.OrdinalIgnoreCase );
					_history = content.History is null
						? new( StringComparer.OrdinalIgnoreCase )
						: new Dictionary<string, List<Point>>( content.History, StringComparer.OrdinalIgnoreCase );
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
				lock( _lock )
				{
					foreach( var h in _history.Values ) Trim( h );
					content = new Content()
					{
						Marks = new Dictionary<string, Mark>( _marks ),
						History = _history.Where( kv => kv.Value.Count > 0 ).ToDictionary( kv => kv.Key, kv => kv.Value.ToList() ),
					};
				}

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
				gone = _marks.Keys.Concat( _history.Keys ).Distinct( StringComparer.OrdinalIgnoreCase ).Where( path => !Exists( path ) ).ToList();
				foreach( var path in gone ) { _marks.Remove( path ); _history.Remove( path ); }
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

		/// <summary>Where a collection "until a time" ends in a file, and what decided it.</summary>
		public class UntilEnd
		{
			/// <summary>Where to end (exclusive); the file's length when it is not cut.</summary>
			public long Offset;

			/// <summary>True if the file is cut at a record rather than taken to its end.</summary>
			public bool Cut;

			/// <summary>The record the collection ends at - its time and kind - or null for the file's end.</summary>
			public Point? To;

			/// <summary>For the archive: what this end is, or why the file may hold more than the window.</summary>
			public string? Note;
		}

		/// <summary>
		/// Where a collection of this file "until" the given time should end: at the first recorded length at or after
		/// that time - a mark, a clear or a checkpoint - so that it holds everything written until then, and a little after at most.
		/// </summary>
		/// <remarks>
		/// The mirror of <see cref="WhereToStartSince"/>, wrong only in the same safe direction: a file that cannot be cut
		/// where it should is taken to its end, with a note saying it may hold more than the window. Too much is fine, too little is not.
		/// </remarks>
		public UntilEnd WhereToEndUntil( string path, long currentLength, DateTime currentCreatedUtc, DateTime lastWriteUtc, DateTime untilUtc )
		{
			var at = $"{untilUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

			if( lastWriteUtc <= untilUtc )
				return new UntilEnd() { Offset = currentLength };

			Point? to, before;
			lock( _lock )
			{
				var h = _history.TryGetValue( path, out var list ) ? list : null;
				to = h?.FirstOrDefault( p => p.AtUtc >= untilUtc );
				before = h?.LastOrDefault( p => p.AtUtc < untilUtc );
			}

			if( to is null )
				return new UntilEnd() { Offset = currentLength, Note = $"to the end of the file, which may hold more than the window: there is no record of its length at or after {at}" };

			var drawn = $"{to.MadeBy} of {to.AtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

			// the same checks as for a mark - see WhereToStart; a length that dropped between the records
			// means the offsets of the file before and after are not the same offsets
			if( to.CreatedUtc != currentCreatedUtc || currentLength < to.Offset
				|| ( before is not null && before.CreatedUtc == to.CreatedUtc && before.Offset > to.Offset )
				|| !FingerprintMatches( path, new Mark() { Offset = to.Offset, Fingerprint = to.Fingerprint } ) )
				return new UntilEnd() { Offset = currentLength, Note = $"to the end of the file, which may hold more than the window: it was replaced, truncated or rotated around the {drawn}" };

			return new UntilEnd() { Offset = to.Offset, Cut = true, To = to,
									Note = $"written up to the {drawn} (byte {to.Offset}), the first record at or after {at}" };
		}

		/// <summary>Where a collection "since a time" starts in a file, and what decided it.</summary>
		public class SinceStart
		{
			/// <summary>Nothing was written to the file since the time: it is left out.</summary>
			public bool Skip;

			/// <summary>Where to start; 0 for the whole file.</summary>
			public long Offset;

			/// <summary>For the archive: what this part of the file is, or why the whole file came.</summary>
			public string? Note;

			/// <summary>The record the collection starts at - its time and kind - or null for the whole file.</summary>
			public Point? From;
		}

		/// <summary>
		/// Where a collection of this file "since" the given time should start: at the latest recorded length at or before
		/// that time - a mark or a checkpoint - so that it holds everything written since, and a little before at most.
		/// </summary>
		/// <remarks>
		/// Nothing is read from inside the file: the time comes from the records and from the file system. Wrong only
		/// in the safe direction - a file that cannot be cut where it should is collected whole, with a note saying why.
		/// </remarks>
		public SinceStart WhereToStartSince( string path, long currentLength, DateTime currentCreatedUtc, DateTime lastWriteUtc, DateTime sinceUtc )
		{
			var at = $"{sinceUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

			if( lastWriteUtc < sinceUtc )
				return new SinceStart() { Skip = true, Note = $"nothing has been written to it since {at}" };

			if( currentCreatedUtc > sinceUtc )
				return new SinceStart() { Note = $"the whole file: it was created after {at}" };

			Point? from;
			lock( _lock )
			{
				from = _history.TryGetValue( path, out var h ) ? h.LastOrDefault( p => p.AtUtc <= sinceUtc ) : null;
			}

			if( from is null )
				return new SinceStart() { Note = $"the whole file: there is no record of its length at or before {at}" };

			var drawn = $"{from.MadeBy} of {from.AtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

			// the same checks as for a mark - see WhereToStart
			if( from.CreatedUtc != currentCreatedUtc || currentLength < from.Offset
				|| !FingerprintMatches( path, new Mark() { Offset = from.Offset, Fingerprint = from.Fingerprint } ) )
				return new SinceStart() { Note = $"the whole file: it was replaced, truncated or rotated since the {drawn}" };

			if( from.Offset <= 0 ) return new SinceStart() { From = from };

			return new SinceStart() { Offset = from.Offset, From = from,
									  Note = $"written after the {drawn} (byte {from.Offset}), the last record at or before {at}" };
		}
	}
}
