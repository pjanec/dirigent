using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Web
{
	/// <summary>One version found on the site, and everything that has it.</summary>
	public class AppVersionEntry
	{
		/// <summary>The Component of the applications; null for applications that name none.</summary>
		public string? component { get; set; }

		/// <summary>"resource" (the version resource of the executables), the path of the version file next to them, or null.</summary>
		public string? source { get; set; }

		/// <summary>The version as found, split into its lines - nothing else is done to it; null where there is none.</summary>
		public List<string>? version { get; set; }

		/// <summary>Why there is no version: the program is not there, it carries none, its machine did not answer. Null when there is one.</summary>
		public string? missing { get; set; }

		public List<string> machines { get; set; } = new();
		public List<string> apps { get; set; } = new();
	}

	/// <summary>
	/// The versions of the applications of a site, each listed once: the locations of all the applications (see
	/// <see cref="AppLocationQuery"/>) grouped by component, source and text. The text is not interpreted - only split
	/// into lines - so two entries differ exactly when what is on the disk differs.
	/// </summary>
	public static class AppVersions
	{
		public static List<AppVersionEntry> Of( IEnumerable<AppLocationEntry> locations )
		{
			return ( from l in locations
					 let missing = Missing( l )
					 group l by ( l.component, source: missing is null ? l.exe.versionSource : null, text: missing is null ? l.exe.fileVersion : null, missing ) into g
					 orderby g.Key.component is null, g.Key.component, g.Key.missing is not null, g.Key.missing, g.Key.source, g.Key.text
					 select new AppVersionEntry()
					 {
						 component = g.Key.component,
						 source = g.Key.source,
						 version = g.Key.text?.Split( '\n' ).Select( x => x.TrimEnd( '\r' ) ).ToList(),
						 missing = g.Key.missing,
						 machines = g.Select( x => x.machine ).Distinct().OrderBy( x => x, StringComparer.OrdinalIgnoreCase ).ToList(),
						 apps = g.Select( x => x.app ).OrderBy( x => x, StringComparer.OrdinalIgnoreCase ).ToList(),
					 } ).ToList();
		}

		/// <summary>Why an application has no version; null if it has one.</summary>
		static string? Missing( AppLocationEntry l )
		{
			if( !string.IsNullOrEmpty( l.exe.fileVersion ) ) return null;
			if( l.error is not null ) return l.error;
			if( l.kind == "dirigentCommand" ) return "a Dirigent command, no program";
			if( !l.exe.exists ) return "the program is not there";
			return "the program carries no version, and there is no version file next to it";
		}
	}
}
