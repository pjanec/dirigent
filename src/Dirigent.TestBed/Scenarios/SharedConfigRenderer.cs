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
				root.Add( RenderPlan( plan, ctx ) );

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

		static XElement RenderApp( AppSpec app, RenderContext ctx )
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

			foreach( var node in app.VfsNodes )
				element.Add( RenderVfsNode( node, ctx, app.MachineName, app.AppId ) );

			foreach( var xml in app.ExtraXml )
				foreach( var child in ParseFragment( Subst( xml ) ) )
					element.Add( child );

			return element;
		}

		static XElement RenderPlan( PlanSpec plan, RenderContext ctx )
		{
			var element = new XElement( "Plan", new XAttribute( "Name", plan.Name ) );

			foreach( var (name, value) in plan.Attributes )
				element.SetAttributeValue( name, value );

			foreach( var app in plan.Apps )
			{
				var appElement = new XElement( "App",
					new XAttribute( "AppIdTuple", new AppIdTuple( ctx.MachineId( app.MachineName ), app.AppId ).ToString() ) );

				foreach( var (name, value) in app.Attributes )
					appElement.SetAttributeValue( name, value );

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
