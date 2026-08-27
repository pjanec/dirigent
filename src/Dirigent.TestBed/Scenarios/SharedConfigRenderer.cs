using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Dirigent.TestBed.Scenarios
{
	/// <summary>
	/// Turns a scenario into a SharedConfig.xml. Built with XElement rather than string
	/// concatenation so that Windows paths, quotes and masks are escaped correctly.
	/// </summary>
	public static class SharedConfigRenderer
	{
		public static string Render( ScenarioSpec spec, RenderContext ctx )
			=> RenderToXml( spec, ctx ).ToString();

		public static XElement RenderToXml( ScenarioSpec spec, RenderContext ctx )
		{
			var root = new XElement( "Shared" );

			foreach( var machine in spec.Machines )
				root.Add( RenderMachine( machine, ctx ) );

			foreach( var app in spec.Apps )
				root.Add( RenderApp( app, ctx ) );

			foreach( var plan in spec.Plans )
				root.Add( RenderPlan( plan, spec, ctx ) );

			foreach( var package in spec.Packages )
				root.Add( RenderPackage( package, ctx ) );

			foreach( var xml in spec.ExtraSharedXml )
				foreach( var element in ParseFragment( ctx.Substitute( xml ) ) )
					root.Add( element );

			return root;
		}

		static XElement RenderMachine( MachineSpec machine, RenderContext ctx )
		{
			var element = new XElement( "Machine",
				new XAttribute( "Name", ctx.MachineId( machine.Name ) ),
				new XAttribute( "IP", machine.Ip ) );

			foreach( var (name, path) in machine.Shares )
			{
				element.Add( new XElement( "Share",
					new XAttribute( "Name", name ),
					new XAttribute( "Path", ctx.Substitute( path, machine.Name ) ) ) );
			}

			return element;
		}

		/// <param name="includeVfsNodes">
		/// False for a plan's copy of an application: the nodes are already declared on the standalone
		/// definition, and declaring them twice would register the same id twice.
		/// </param>
		static XElement RenderApp( AppSpec app, RenderContext ctx, bool includeVfsNodes = true )
		{
			string Subst( string text ) => ctx.Substitute( text, app.MachineName, app.AppId );

			var element = new XElement( "App",
				new XAttribute( "AppIdTuple", app.Id( ctx ).ToString() ),
				new XAttribute( "ExeFullPath", app.ExeFullPath is null ? ctx.TestAppPath : Subst( app.ExeFullPath ) ),
				new XAttribute( "CmdLineArgs", Subst( app.CmdLineArgs ) ),
				new XAttribute( "StartupDir", app.StartupDir is null
												? ctx.AppDir( app.MachineName, app.AppId )
												: Subst( app.StartupDir ) ),
				new XAttribute( "WindowStyle", WindowStyleName( app.WindowStyle ) ) );

			foreach( var (name, value) in app.Attributes )
				element.SetAttributeValue( name, Subst( value ) );

			if( app.EnvVars.Count > 0 )
			{
				var env = new XElement( "Env" );
				foreach( var (name, value) in app.EnvVars )
				{
					env.Add( new XElement( "Set",
						new XAttribute( "Variable", name ),
						new XAttribute( "Value", Subst( value ) ) ) );
				}
				element.Add( env );
			}

			if( includeVfsNodes )
			{
				foreach( var node in app.VfsNodes )
					element.Add( RenderVfsNode( node, ctx, app.MachineName, app.AppId ) );
			}

			foreach( var xml in app.ExtraXml )
				foreach( var child in ParseFragment( Subst( xml ) ) )
					element.Add( child );

			return element;
		}

		/// <remarks>
		/// A plan's &lt;App&gt; is a complete application definition, not a reference to the standalone
		/// one - without an executable Dirigent tries to launch the startup folder and reports "Access
		/// is denied". So each entry is the application rendered again, with the plan's own attributes
		/// (dependencies, init condition, volatility) laid over it.
		/// </remarks>
		static XElement RenderPlan( PlanSpec plan, ScenarioSpec spec, RenderContext ctx )
		{
			var element = new XElement( "Plan", new XAttribute( "Name", plan.Name ) );

			foreach( var (name, value) in plan.Attributes )
				element.SetAttributeValue( name, value );

			foreach( var planApp in plan.Apps )
			{
				var app = spec.Apps.FirstOrDefault(
							a => a.MachineName == planApp.MachineName && a.AppId == planApp.AppId )
					?? throw new ArgumentException(
						$"plan '{plan.Name}' names {planApp.MachineName}.{planApp.AppId}, which is not part of this scenario" );

				var appElement = RenderApp( app, ctx, includeVfsNodes: false );

				foreach( var (name, value) in planApp.Attributes )
					appElement.SetAttributeValue( name, ctx.Substitute( value, app.MachineName, app.AppId ) );

				element.Add( appElement );
			}

			return element;
		}
		static XElement RenderPackage( PackageSpec package, RenderContext ctx )
		{
			var element = new XElement( "FilePackage", new XAttribute( "Id", package.Id ) );
			if( package.Title is not null )
				element.SetAttributeValue( "Title", package.Title );

			foreach( var child in package.Children )
				element.Add( RenderVfsNode( child, ctx, null, null ) );

			return element;
		}

		static XElement RenderVfsNode( VfsSpec node, RenderContext ctx, string? machineName, string? appId )
		{
			string? Subst( string? text ) => text is null ? null : ctx.Substitute( text, machineName, appId );

			switch( node.Kind )
			{
				case VfsKind.NewestFiles:
				{
					var element = new XElement( "File",
						new XAttribute( "Id", node.Id ),
						new XAttribute( "Path", Subst( node.Path ) ?? "" ),
						new XAttribute( "Filter", "Newest" ) );

					if( node.Title is not null ) element.SetAttributeValue( "Title", node.Title );
					if( node.Mask is not null ) element.SetAttributeValue( "Mask", node.Mask );
					if( node.MaxFiles.HasValue ) element.SetAttributeValue( "MaxFiles", node.MaxFiles.Value );
					if( node.MaxSeconds.HasValue ) element.SetAttributeValue( "MaxSeconds", Inv( node.MaxSeconds.Value ) );
					return element;
				}

				case VfsKind.Folder:
				{
					var element = new XElement( "Folder",
						new XAttribute( "Id", node.Id ),
						new XAttribute( "Path", Subst( node.Path ) ?? "" ) );

					if( node.Title is not null ) element.SetAttributeValue( "Title", node.Title );
					if( node.Mask is not null ) element.SetAttributeValue( "Mask", node.Mask );
					if( node.MaxFiles.HasValue ) element.SetAttributeValue( "MaxFiles", node.MaxFiles.Value );
					if( node.MaxSeconds.HasValue ) element.SetAttributeValue( "MaxSeconds", Inv( node.MaxSeconds.Value ) );
					if( node.MaxTotalBytes.HasValue ) element.SetAttributeValue( "MaxTotalBytes", node.MaxTotalBytes.Value );
					return element;
				}

				case VfsKind.Ref:
				{
					var element = new XElement( "FileRef", new XAttribute( "Id", node.Id ) );

					// "*" and "" are wildcards and stay as they are; anything else names a machine
					// of the scenario and has to become that machine's real id
					if( node.RefMachineId is not null )
					{
						element.SetAttributeValue( "MachineId",
							node.RefMachineId is "*" or "" ? node.RefMachineId : ctx.MachineId( node.RefMachineId ) );
					}

					if( node.RefAppId is not null )
						element.SetAttributeValue( "AppId", node.RefAppId );

					return element;
				}

				default:
					throw new NotSupportedException( $"unsupported VFS node kind {node.Kind}" );
			}
		}

		static IEnumerable<XElement> ParseFragment( string xml )
			=> XElement.Parse( $"<fragment>{xml}</fragment>" ).Elements();

		static string WindowStyleName( WindowStyleSpec style ) => style switch
		{
			WindowStyleSpec.Minimized => "minimized",
			WindowStyleSpec.Normal => "normal",
			WindowStyleSpec.Maximized => "maximized",
			WindowStyleSpec.Hidden => "hidden",
			_ => "minimized",
		};

		static string Inv( double d ) => d.ToString( CultureInfo.InvariantCulture );
	}
}
