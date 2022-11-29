using System.Collections.Generic;

namespace Dirigent
{
	/// <summary>
	/// Gathers the file defs during dirigent's config file parsing
	/// </summary>
	public class FileDefReg
	{
		/// <summary>
		/// All FileDefs found across the SharedConfig file
		/// </summary>
		List<FileDef> _files = new List<FileDef>();
		public IEnumerable<FileDef> Files => _files;
		
		/// <summary>
		/// All FilePackageDefs found across the SharedConfig file
		/// </summary>
		List<FilePackageDef> _packages = new List<FilePackageDef>();
		public IEnumerable<FilePackageDef> Packages => _packages;

		public FileDef Add( FileDef fileDef )
		{
			// if already defined with same attribs, use the existing
			foreach( var x in _files )
			{
				if( x.SameAs( fileDef ) )
				{
					return x;
				}
			}
			// add
			_files.Add( fileDef );
			return fileDef;;
		}


	}
}
