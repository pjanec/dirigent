using System;

namespace Dirigent
{
	/// <summary>
	/// A time window (UTC) that chooses the files of Clearable file definitions: since and/or until.
	/// A file is in the window when its written span overlaps it.
	/// </summary>
	/// <remarks>
	/// NTFS file-system tunnelling can make the CreationTime of a recreated file older than the real one.
	/// That errs towards taking too much, which is the allowed direction.
	/// </remarks>
	public class FileWindow
	{
		public DateTime? SinceUtc;
		public DateTime? UntilUtc;

		/// <summary>The file was written (creation..last write) at some moment within the window.</summary>
		public bool Overlaps( DateTime creationUtc, DateTime lastWriteUtc )
			=> ( SinceUtc is null || lastWriteUtc >= SinceUtc ) && ( UntilUtc is null || creationUtc <= UntilUtc );
	}
}
