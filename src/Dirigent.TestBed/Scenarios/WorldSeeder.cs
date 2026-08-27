using System;
using System.IO;
using System.Text;

namespace Dirigent.TestBed.Scenarios
{
	/// <summary>
	/// Creates the folders and files a scenario says should already exist. Ages are applied to the
	/// last-write time, because that is what Dirigent's file filters go by.
	/// </summary>
	public static class WorldSeeder
	{
		public static void Seed( ScenarioSpec spec, RenderContext ctx )
		{
			// every application gets its own working folder and log folder, so that log files of
			// different apps cannot be confused for one another
			foreach( var app in spec.Apps )
			{
				Directory.CreateDirectory( ctx.AppDir( app.MachineName, app.AppId ) );
				Directory.CreateDirectory( ctx.AppLogsDir( app.MachineName, app.AppId ) );
			}

			foreach( var machine in spec.Machines )
				Directory.CreateDirectory( ctx.MachineDir( machine.Name ) );

			foreach( var seed in spec.Seeds )
				WriteSeed( seed, ctx );
		}

		static void WriteSeed( SeedSpec seed, RenderContext ctx )
		{
			var folder = string.IsNullOrEmpty( seed.AppId )
							? ctx.MachineDir( seed.MachineName )
							: ctx.AppLogsDir( seed.MachineName, seed.AppId );

			Directory.CreateDirectory( folder );

			var path = Path.Combine( folder, seed.FileName );

			if( seed.Content is not null )
			{
				File.WriteAllText( path, seed.Content, Encoding.UTF8 );
			}
			else
			{
				// recognisable filler, so a file that turns up in an archive can be traced back
				var line = $"seeded {seed.MachineName}.{seed.AppId} {seed.FileName} age={seed.AgeDays}d";
				var content = new StringBuilder( line );
				while( content.Length < seed.SizeBytes ) content.Append( '.' );
				File.WriteAllText( path, content.ToString(), Encoding.UTF8 );
			}

			var when = DateTime.UtcNow.AddDays( -seed.AgeDays );
			File.SetLastWriteTimeUtc( path, when );
			File.SetCreationTimeUtc( path, when );
		}
	}
}
