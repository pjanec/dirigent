using System;
using System.IO;
using System.Text;

namespace Dirigent
{
	/// <summary>
	/// Taking just the end of a file, for the log files too big to collect whole.
	/// </summary>
	/// <remarks>
	/// A logger that never rotates produces files of tens of gigabytes, which is not something a
	/// download can carry - and what an investigation needs is almost always the end of them. The
	/// tail is therefore not a compromise but the useful part; what matters is that nobody mistakes
	/// it for the whole file, hence the distinct entry name and the header line.
	/// </remarks>
	public static class FileTail
	{
		/// <summary>How far to look for a line break before giving up and cutting mid-line.</summary>
		const int _maxLineScan = 64 * 1024;

		/// <summary>Whether a file of this size gets truncated under this setting.</summary>
		public static bool Applies( long fileLength, long tailBytes )
			=> tailBytes > 0 && fileLength > tailBytes;

		/// <summary>
		/// How much of the file will actually be collected - what a size budget should count.
		/// </summary>
		public static long EffectiveSize( long fileLength, long tailBytes )
			=> Applies( fileLength, tailBytes ) ? tailBytes : fileLength;

		/// <summary>
		/// The name a truncated file gets, so that the truncation is visible in the archive listing.
		/// </summary>
		public static string EntryNameFor( string fileName, long tailBytes )
		{
			var stem = Path.GetFileNameWithoutExtension( fileName );
			var ext = Path.GetExtension( fileName );
			return $"{stem}.last{FormatSize( tailBytes )}{ext}";
		}

		/// <summary>
		/// The line put at the top of a truncated entry. Whoever opens the archive months later has
		/// only what is inside it to go by.
		/// </summary>
		public static string HeaderFor( string filePath, long fileLength, long takenBytes, DateTime when )
			=> $"*** Dirigent: this is the last {takenBytes} bytes of {filePath},"
				+ $" which was {fileLength} bytes at {when:yyyy-MM-dd HH:mm:ss}."
				+ $" The earlier part of the file is not included. ***" + Environment.NewLine;

		/// <summary>
		/// Positions the stream where the tail should start, at a line boundary, and returns that
		/// offset.
		/// </summary>
		public static long SeekToTailStart( Stream stream, long tailBytes )
			=> SeekToStart( stream, stream.Length - tailBytes );

		/// <summary>
		/// The raw offset a tail of this size begins at - what a mark's offset is weighed against.
		/// </summary>
		public static long RawTailStart( long fileLength, long tailBytes )
			=> Applies( fileLength, tailBytes ) ? fileLength - tailBytes : 0;

		/// <summary>
		/// Positions the stream at the first line boundary at or after the given offset, and returns
		/// that offset. Cutting at the raw byte offset would make the first line garbage.
		/// </summary>
		/// <remarks>
		/// A file with no line break anywhere near the cut - a binary one, most likely - is cut at
		/// the raw offset; a partial copy of such a file is questionable anyway, and the header says
		/// where it starts.
		/// </remarks>
		public static long SeekToStart( Stream stream, long rawOffset )
		{
			var start = rawOffset;
			if( start <= 0 )
			{
				stream.Position = 0;
				return 0;
			}

			if( start >= stream.Length )
			{
				// nothing new since the mark - an entry with a header and no content
				stream.Position = stream.Length;
				return stream.Length;
			}

			// already at a line boundary? then keep it - skipping forward would drop a whole line
			stream.Position = start - 1;
			if( stream.ReadByte() == '\n' )
				return start;

			// otherwise walk forward to just past the next line break
			var limit = Math.Min( start + _maxLineScan, stream.Length );
			while( stream.Position < limit )
			{
				if( stream.ReadByte() == '\n' )
					return stream.Position;
			}

			stream.Position = start;
			return start;
		}

		/// <summary>
		/// The name a partial file gets when a mark decided where it starts.
		/// </summary>
		public static string MarkedEntryNameFor( string fileName )
		{
			var stem = Path.GetFileNameWithoutExtension( fileName );
			var ext = Path.GetExtension( fileName );
			return $"{stem}.since-mark{ext}";
		}

		/// <summary>
		/// The line put at the top of a partial entry, saying which part of the file it is and why.
		/// </summary>
		public static string PartialHeaderFor( string filePath, long fileLength, long takenBytes,
				DateTime when, string reason )
			=> $"*** Dirigent: this is {takenBytes} bytes of {filePath} - {reason}."
				+ $" The file was {fileLength} bytes at {when:yyyy-MM-dd HH:mm:ss}."
				+ $" The rest of it is not included. ***" + Environment.NewLine;

		/// <summary>
		/// A byte count as a person would write it, for use in a file name - "50MB", "512KB", "300B".
		/// </summary>
		/// <remarks>
		/// Deliberately culture invariant: this ends up in a file name inside an archive, and the
		/// same configuration must not produce "1.5KB" on one machine and "1,5KB" on the next.
		/// </remarks>
		public static string FormatSize( long bytes )
		{
			const long kb = 1024, mb = 1024 * kb, gb = 1024 * mb;
			var inv = System.Globalization.CultureInfo.InvariantCulture;

			if( bytes >= gb && bytes % gb == 0 ) return $"{bytes / gb}GB";
			if( bytes >= mb && bytes % mb == 0 ) return $"{bytes / mb}MB";
			if( bytes >= kb && bytes % kb == 0 ) return $"{bytes / kb}KB";

			// not a round number of units - say it in the largest unit that keeps it readable
			if( bytes >= gb ) return string.Format( inv, "{0:0.#}GB", bytes / (double)gb );
			if( bytes >= mb ) return string.Format( inv, "{0:0.#}MB", bytes / (double)mb );
			if( bytes >= kb ) return string.Format( inv, "{0:0.#}KB", bytes / (double)kb );
			return $"{bytes}B";
		}
	}
}
