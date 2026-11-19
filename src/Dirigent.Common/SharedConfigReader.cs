using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Xml.Linq;
using System.Text.RegularExpressions;

using X = Dirigent.XmlConfigReaderUtils;
using System.Diagnostics;

namespace Dirigent
{


	public class SharedConfigReader
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public SharedConfig Config => _cfg;

		SharedConfig _cfg;
		XDocument _doc;
		XElement _root; // the top level XML element
		FileDefReg _fdReg = new FileDefReg();


		public SharedConfigReader( System.IO.TextReader textReader )
		{
			_cfg = new SharedConfig();
			_doc = XDocument.Load( textReader ); // null should never be returned, exception would be thrown insterad
#pragma warning disable CS8601 // Possible null reference assignment.
			_root = _doc.Element( "Shared" );
#pragma warning restore CS8601 // Possible null reference assignment.
			if ( _root is null ) throw new Exception("SharedConfig missing the root element");

			CheckMinVersion( _root );

			// before anything is read: from here on the file nodes are plain, templates are history
			// machine templates first: the nodes they bring into a machine may use node templates, and the
			// machines are put together only once those are expanded, because a node's Id may come from one
			var machineTemplates = MachineTemplates.Apply( _root );
			NodeTemplates.Apply( _root );
			machineTemplates.Merge();

			LoadAppDefaults();
			LoadPlans();
			CheckDependencies();
			LoadMachines( _fdReg );
			LoadUnboundFiles( _fdReg );
			LoadKillAllExtras();

			_cfg.VfsNodes = _fdReg.VfsNodes;
			_cfg.SingleInstScripts = LoadSingleInstScripts(_root);
			_cfg.MainMenuItems = LoadMainMenuItems(_root);
		}

		/// <summary>
		/// The version of the running Dirigent, compared with a config's MinDirigentVersion. Settable so
		/// that a test can be an older or a newer program. A build that was not stamped with a release
		/// version (1.0.0.0, what a development build reports) is not checked at all.
		/// </summary>
		public static string CurrentVersion { get; set; } = DetectCurrentVersion();

		static string DetectCurrentVersion()
		{
			var asm = typeof( SharedConfigReader ).Assembly;
			var info = asm.GetCustomAttributes( typeof( System.Reflection.AssemblyInformationalVersionAttribute ), false )
							.OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
							.FirstOrDefault()?.InformationalVersion;

			var text = info ?? asm.GetName().Version?.ToString() ?? "";

			var meta = text.IndexOfAny( new[] { '+', '-' } ); // "3.1.19.4+commit"
			return meta >= 0 ? text.Substring( 0, meta ) : text;
		}

		/// <summary>Dotted numbers, missing segments being 0; null if it is not that.</summary>
		static int[]? ParseVersion( string? text )
		{
			if( string.IsNullOrWhiteSpace( text ) ) return null;

			var parts = text.Trim().Split( '.' );
			var numbers = new int[parts.Length];
			for( int i = 0; i < parts.Length; i++ )
				if( !int.TryParse( parts[i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out numbers[i] ) )
					return null;

			return numbers;
		}

		static int CompareVersions( int[] a, int[] b )
		{
			for( int i = 0; i < Math.Max( a.Length, b.Length ); i++ )
			{
				var d = ( i < a.Length ? a[i] : 0 ).CompareTo( i < b.Length ? b[i] : 0 );
				if( d != 0 ) return d;
			}
			return 0;
		}

		/// <summary>
		/// Refuses a config that needs a newer Dirigent than this one. An older program would not fail
		/// on what it does not know - it would ignore a NodeTemplate and collect whole folders.
		/// </summary>
		static void CheckMinVersion( XElement root )
		{
			var wanted = root.Attribute( "MinDirigentVersion" )?.Value;
			if( wanted is null ) return;

			var min = ParseVersion( wanted )
				?? throw new ConfigurationErrorException( $"Invalid MinDirigentVersion '{wanted}' in <Shared>: dotted numbers like 3.1.19 expected." );

			var current = ParseVersion( CurrentVersion );

			// an unstamped development build says 1.0.0.0 and could not be told from an ancient release
			if( current is null || CompareVersions( current, new[] { 1, 0, 0, 0 } ) == 0 ) return;

			if( CompareVersions( current, min ) < 0 )
				throw new ConfigurationErrorException( $"This SharedConfig needs Dirigent {wanted.Trim()} or newer; this is {CurrentVersion}." );
		}

		public static AppDef ReadAppElement( XElement e, XElement root, FileDefReg fdReg, string? machineId, string? appId, IReadOnlyList<string>? templateChain = null )
		{
			AppDef a;

			var idStr = e.Attribute( "AppIdTuple" )?.Value;
			if( !string.IsNullOrEmpty( idStr ) )
			{
				var id = new AppIdTuple( idStr );
				machineId  = id.MachineId;
				appId = id.AppId;
			}

			// first load templates
			var templateName = X.getStringAttr( e, "Template" );
			if( !string.IsNullOrEmpty(templateName) )
			{
				// a template naming itself, directly or through others, would recurse until the stack is gone
				var chain = ( templateChain ?? new List<string>() ).ToList();
				var at = chain.IndexOf( templateName );
				if( at >= 0 )
				{
					var cycleMsg = "AppTemplate cycle: " + string.Join( " -> ", chain.Skip( at ).Append( templateName ) );
					log.Error( cycleMsg );
					throw new ConfigurationErrorException( cycleMsg );
				}

				XElement? te = ( from t in root.Elements( "AppTemplate" )
								where t.Attribute( "Name" )?.Value == templateName
								select t ).FirstOrDefault();

				if( te == null )
				{
					// FIXME: tog that template is missing
					var msg = String.Format( "Template '{0}' not found", templateName );
					log.ErrorFormat( msg );
					throw new ConfigurationErrorException( msg );
					//a = new AppDef();
				}
				else
				{
					// the nodes of a template are only a starting point - a derived template or the app may replace
					// some - so they are registered once the app has its final list, not here
					a = ReadAppElement( te, root, new FileDefReg(), machineId, appId, chain.Append( templateName ).ToList() );
				}
			}
			else
			{
				a = new AppDef();
			}

			// read element content into memory, apply defaults
			var x = new
			{
				Id = e.Attribute( "AppIdTuple" )?.Value,
				ExeFullPath = e.Attribute( "ExeFullPath" )?.Value,
				StartupDir = e.Attribute( "StartupDir" )?.Value,
				CmdLineArgs = e.Attribute( "CmdLineArgs" )?.Value,
				StartupOrder = e.Attribute( "StartupOrder" )?.Value,
				Icon = e.Attribute( "Icon" )?.Value,
				Disabled = e.Attribute( "Disabled" )?.Value,
				Volatile = e.Attribute( "Volatile" )?.Value,
				DeElevate = e.Attribute( "DeElevate" )?.Value,
				ReusePrevVars = e.Attribute( "ReusePrevVars" )?.Value,
				LeaveRunningWithPrevVars = e.Attribute( "LeaveRunningWithPrevVars" )?.Value,
				RestartOnCrash = e.Attribute( "RestartOnCrash" )?.Value,
				AdoptIfAlreadyRunning = e.Attribute( "AdoptIfAlreadyRunning" )?.Value,
				PriorityClass = e.Attribute( "PriorityClass" )?.Value,
				InitCondition = e.Attribute( "InitCondition" )?.Value,
				SeparationInterval = e.Attribute( "SeparationInterval" )?.Value,
				MinKillingTime = e.Attribute( "MinKillingTime" )?.Value,
				Dependecies = e.Attribute( "Dependencies" )?.Value,
				KillTree = e.Attribute( "KillTree" )?.Value,
				KillSoftly = e.Attribute( "KillSoftly" )?.Value,
				WindowStyle = e.Attribute( "WindowStyle" )?.Value,
				WindowPos = e.Elements( "WindowPos" ),
				Restarter = e.Element( "Restarter" ),
				SoftKill = e.Element( "SoftKill" ),
				Env = e.Element( "Env" ),
				InitDetectors = e.Element( "InitDetectors" )?.Elements(),
				Groups = e.Attribute( "Groups" )?.Value,
				Actions = new List<ActionDef>(),
			};

			// then overwrite templated values with current content
			if( x.Id != null ) a.Id = new AppIdTuple( x.Id );
			if( x.ExeFullPath != null ) a.ExeFullPath = x.ExeFullPath;
			if( x.StartupDir != null ) a.StartupDir = x.StartupDir;
			if( x.CmdLineArgs != null ) a.CmdLineArgs = x.CmdLineArgs;
			if( x.StartupOrder != null ) a.StartupOrder = int.Parse( x.StartupOrder );
			if( x.Icon != null ) a.Icon = x.Icon;
			if( x.Disabled != null ) a.Disabled = ( int.Parse( x.Disabled ) != 0 );
			if( x.Volatile != null ) a.Volatile = ( int.Parse( x.Volatile ) != 0 );
			if( x.DeElevate != null ) a.DeElevate = ( int.Parse( x.DeElevate ) != 0 );
			if( x.ReusePrevVars != null ) a.ReusePrevVars = ( int.Parse( x.ReusePrevVars ) != 0 );
			if( x.LeaveRunningWithPrevVars != null ) a.LeaveRunningWithPrevVars = ( int.Parse( x.LeaveRunningWithPrevVars ) != 0 );
			if( x.RestartOnCrash != null ) a.RestartOnCrash = ( int.Parse( x.RestartOnCrash ) != 0 );
			if( x.AdoptIfAlreadyRunning != null ) a.AdoptIfAlreadyRunning = ( int.Parse( x.AdoptIfAlreadyRunning ) != 0 );
			if( x.PriorityClass != null ) a.PriorityClass = x.PriorityClass;
			if( x.InitCondition != null ) a.InitializedCondition = x.InitCondition;
			if( x.SeparationInterval != null ) a.SeparationInterval = double.Parse( x.SeparationInterval, CultureInfo.InvariantCulture );
			if( x.MinKillingTime != null ) a.MinKillingTime = double.Parse( x.MinKillingTime, CultureInfo.InvariantCulture );
			if( x.Dependecies != null )
			{
				var deps = new List<string>();
				foreach( var d in x.Dependecies.Split( ';' ) )
				{
					var stripped = d.Trim();
					if( stripped != "" )
					{
						deps.Add( d );
					}

				}
				a.Dependencies = deps;
			}

			if( x.KillTree != null ) a.KillTree = ( int.Parse( x.KillTree ) != 0 );

			if( !String.IsNullOrEmpty( x.KillSoftly ) ) a.KillSoftly = ( int.Parse( x.KillSoftly ) != 0 );

			if( x.WindowStyle != null )
			{
				if( x.WindowStyle.ToLower() == "minimized" ) a.WindowStyle = EWindowStyle.Minimized;
				else if( x.WindowStyle.ToLower() == "maximized" ) a.WindowStyle = EWindowStyle.Maximized;
				else if( x.WindowStyle.ToLower() == "normal" ) a.WindowStyle = EWindowStyle.Normal;
				else if( x.WindowStyle.ToLower() == "hidden" ) a.WindowStyle = EWindowStyle.Hidden;
			}

			if( x.WindowPos != null )
			{
				foreach( var elem in x.WindowPos )
				{
					a.WindowPosXml.Add( elem.ToString() );
				}
			}

			if( x.Restarter != null )
			{
				a.RestarterXml = x.Restarter.ToString();
			}

			if( x.SoftKill != null )
			{
				a.SoftKillXml = x.SoftKill.ToString();
			}

			if( x.Env != null )
			{
				foreach( var elem in x.Env.Descendants() )
				{
					if( elem.Name == "Set" )
					{
						// add/overwite variable
						var variable = elem.Attribute( "Variable" )?.Value;
						var value = elem.Attribute( "Value" )?.Value;
						
						if( !string.IsNullOrEmpty(variable) && value != null )
							a.EnvVarsToSet[variable] = value;
					}

					if( elem.Name == "Local" )
					{
						// add/overwite variable
						var variable = elem.Attribute( "Variable" )?.Value;
						var value = elem.Attribute( "Value" )?.Value;

						if( !string.IsNullOrEmpty(variable) && value != null )
							a.LocalVarsToSet[variable] = value;
					}

					if( elem.Name == "Path" )
					{
						// extend
						var toAppend = elem.Attribute( "Append" )?.Value;
						if( !string.IsNullOrEmpty( toAppend ) )
						{
							if( String.IsNullOrEmpty( a.EnvVarPathToAppend ) )
							{
								a.EnvVarPathToAppend = toAppend;
							}
							else
							{
								a.EnvVarPathToAppend = a.EnvVarPathToAppend + ";" + toAppend;
							}
						}

						var toPrepend = elem.Attribute( "Prepend" )?.Value;
						if( !string.IsNullOrEmpty( toPrepend ) )
						{
							if( String.IsNullOrEmpty( a.EnvVarPathToPrepend ) )
							{
								a.EnvVarPathToPrepend = toPrepend;
							}
							else
							{
								a.EnvVarPathToPrepend = toPrepend + ";" + a.EnvVarPathToPrepend;
							}
						}
					}

				}
			}

			if( x.InitDetectors != null )
			{
				foreach( var elem in x.InitDetectors )
				{
					a.InitDetectors.Add( elem.ToString() );
				}
			}

			if( !string.IsNullOrWhiteSpace(x.Groups) )
			{
				if( !string.IsNullOrEmpty(a.Groups) ) a.Groups += ";";
				a.Groups += x.Groups;
			}

			// A node of this element with the same Id as an inherited one (from a template) replaces it, in the
			// inherited node's place - the same rule as for a machine's, see MachineTemplates. Only if several
			// inherited nodes share that Id must the Title be the same as well; one without an Id is always
			// added. Only the inherited nodes can be replaced, not the ones this element declares itself.
			var inheritedCount = a.VfsNodes.Count;
			foreach ( var vfsNode in LoadVfsNodes( e, machineId, appId ) )
			{
				var replaces = -1;
				if( vfsNode.Id != vfsNode.Guid.ToString() ) // an Id was written
				{
					var sameId = Enumerable.Range( 0, inheritedCount ).Where( i => a.VfsNodes[i].Id == vfsNode.Id ).ToList();

					if( sameId.Count == 1 )
					{
						replaces = sameId[0];
					}
					else
					if( sameId.Count > 1 )
					{
						var title = ExpandAppTitleVars( vfsNode.Title, a );
						var sameTitle = sameId.Where( i => ExpandAppTitleVars( a.VfsNodes[i].Title, a ) == title ).ToList();
						if( sameTitle.Count > 0 ) replaces = sameTitle[0];
					}
				}

				if( replaces >= 0 )
					a.VfsNodes[replaces] = vfsNode;
				else
					a.VfsNodes.Add( vfsNode );
			}

			// The element that is not a template has the final list: titles take the app's own variables now,
			// and the registry - which may know the same node already, from another read of this app - is
			// told about exactly the nodes the app has.
			if( templateChain is null )
			{
				for( int i = 0; i < a.VfsNodes.Count; i++ )
				{
					ExpandTitlesOfNode( a.VfsNodes[i], a );
					a.VfsNodes[i] = fdReg.Add( a.VfsNodes[i] );
				}
			}


			var actions = LoadActions( e, a.Id.MachineId, a.Id.AppId );

			// add/replace actions
			foreach( var item in actions )
			{
				int found = a.Actions.FindIndex( i => i.Id == item.Id );
				if( found < 0 )
				{
					a.Actions.Add( item );
				}
				else // replace
				{
					a.Actions[found] = item;
				}
			}

			ValidateInitConditions( a );

			return a;
		}

		/// <summary>
		/// Refuses a config asking for something that can never happen.
		/// </summary>
		/// <remarks>
		/// `cliresponse` waits for the master's answer to a dirigent command, so on an app that sends
		/// none it would hold the app - and everything depending on it - for ever. A loud failure at
		/// load time is the kinder outcome: on startup a bad config already stops the master, and on
		/// ReloadSharedConfig the previous config stays in effect and the requestor is told why.
		///
		/// Nothing existing can trip over this: the condition is new, so no config in the field uses it.
		/// </remarks>
		static void ValidateInitConditions( AppDef a )
		{
			foreach( var (name, value) in InitConditionsOf( a ) )
			{
				if( !string.Equals( name, InitConditions.CliResponse, StringComparison.OrdinalIgnoreCase ) )
					continue;

				if( !string.Equals( a.ExeFullPath?.Trim(), ReservedExeNames.DirigentCommand,
									StringComparison.OrdinalIgnoreCase ) )
				{
					throw new Exception(
						$"App '{a.Id}': the init condition '{InitConditions.CliResponse}' waits for the answer"
						+ $" to a dirigent command, so it only makes sense with"
						+ $" ExeFullPath=\"{ReservedExeNames.DirigentCommand}\" - this app has"
						+ $" \"{a.ExeFullPath}\"." );
				}

				if( !string.Equals( value, InitConditions.Ok, StringComparison.OrdinalIgnoreCase )
					&& !string.Equals( value, InitConditions.Any, StringComparison.OrdinalIgnoreCase ) )
				{
					throw new Exception(
						$"App '{a.Id}': the init condition '{InitConditions.CliResponse}' needs a value -"
						+ $" '{InitConditions.Ok}' (every command must succeed) or '{InitConditions.Any}'"
						+ $" (any answer will do) - not '{value}'." );
				}
			}
		}

		/// <summary>
		/// The init conditions of an app, as name and value, however they were written: the
		/// InitCondition attribute ("name value") and the &lt;InitDetectors&gt; elements alike.
		/// </summary>
		static IEnumerable<(string Name, string Value)> InitConditionsOf( AppDef a )
		{
			if( !string.IsNullOrWhiteSpace( a.InitializedCondition ) )
			{
				var text = a.InitializedCondition.Trim();
				var space = text.IndexOf( ' ' );

				yield return space < 0
					? ( text, string.Empty )
					: ( text.Substring( 0, space ), text.Substring( space + 1 ).Trim() );
			}

			foreach( var xml in a.InitDetectors )
			{
				XElement element;
				try { element = XElement.Parse( xml ); }
				catch { continue; } // not our business here; the factory will complain when it runs
				yield return ( element.Name.LocalName, element.Value.Trim() );
			}
		}

		/// <summary>
		/// What an app's own configuration can say about a title when the config is loaded: its
		/// <c>&lt;Local&gt;</c> variables, and <c>%APP_STARTUPDIR_NAME%</c> - the last folder of its StartupDir as written
		/// - provided that StartupDir needs nothing more than those variables. A token that cannot be
		/// worked out stays literal.
		/// </summary>
		static string ExpandAppTitleVars( string? title, AppDef app )
		{
			if( string.IsNullOrEmpty( title ) || title.IndexOf( '%' ) < 0 ) return title ?? string.Empty;

			string WithLocals( string text )
			{
				foreach( var (name, value) in app.LocalVarsToSet )
					text = Regex.Replace( text, "%" + Regex.Escape( name ) + "%", value.Replace( "$", "$$" ), RegexOptions.IgnoreCase );
				return text;
			}

			title = WithLocals( title );

			if( title.IndexOf( "%APP_STARTUPDIR_NAME%", StringComparison.OrdinalIgnoreCase ) >= 0 )
			{
				var startupDir = WithLocals( app.StartupDir ?? string.Empty );
				if( startupDir.IndexOf( '%' ) < 0 )
				{
					title = Regex.Replace( title, "%APP_STARTUPDIR_NAME%", AppLaunchSpec.LastSegment( startupDir ).Replace( "$", "$$" ), RegexOptions.IgnoreCase );
				}
			}

			return title;
		}

		static void ExpandTitlesOfNode( VfsNodeDef node, AppDef app )
		{
			node.Title = ExpandAppTitleVars( node.Title, app );
			foreach( var child in node.Children )
				ExpandTitlesOfNode( child, app );
		}

		static string ExpandTitleIds( string? title, string? machineId, string? appId )
		{
			if( string.IsNullOrEmpty( title ) ) return title ?? string.Empty;
			if( !string.IsNullOrEmpty( machineId ) && machineId != "*" )
				title = Regex.Replace( title, "%MACHINE_ID%", machineId.Replace( "$", "$$" ), RegexOptions.IgnoreCase );
			if( !string.IsNullOrEmpty( appId ) && appId != "*" )
				title = Regex.Replace( title, "%APP_ID%", appId.Replace( "$", "$$" ), RegexOptions.IgnoreCase );
			return title;
		}

		static void FillAssocItem( ref AssocMenuItemDef a, XElement e, string? machineId=null, string? appId=null )
		{
			a.MachineId = machineId;
			a.AppId = appId;

			var x = new
			{
				Guid = e.Attribute( "Guid" )?.Value,
				Id = e.Attribute( "Id" )?.Value,
				Title = e.Attribute( "Title" )?.Value,
				AppIdTuple = e.Attribute( "AppIdTuple" )?.Value,
				MachineId = e.Attribute( "MachineId" )?.Value,
				AppId = e.Attribute( "AppId" )?.Value,
				Groups = e.Attribute( "Groups" )?.Value,
				Icon = e.Attribute( "Icon" )?.Value,
				Description = e.Attribute( "Description" )?.Value,
				Actions = LoadActions( e, machineId, appId ),
			};

			// if Guid present, use it, otherwise generate unique one
			if( x.Guid is not null )
			{
				if (!Guid.TryParse( x.Guid, out a.Guid ))
				{
					throw new Exception( $"Invalid Guid in {e}" );
				}
			}
			else
			{
				a.Guid = Guid.NewGuid();
			}

			// string Id by default matches the Guid, can be overridden by Id attribute
			a.Id = a.Guid.ToString();
			if( x.Id is not null ) a.Id = x.Id;
			
			if( x.Title != null ) a.Title = x.Title;

			// "AppIdTuple" sets both the machine and the app at once;
			// the individual "MachineId"/"AppId" attributes (if any) still win over it
			if( !string.IsNullOrEmpty( x.AppIdTuple ) )
			{
				var idTuple = new AppIdTuple( x.AppIdTuple );
				a.MachineId = idTuple.MachineId;
				a.AppId = idTuple.AppId;
			}

			if( x.MachineId != null ) a.MachineId = x.MachineId;
			if( x.AppId != null ) a.AppId = x.AppId;
			if( x.Icon != null ) a.Icon = x.Icon;
			if( x.Groups != null ) a.Groups = x.Groups;
			if( x.Description != null ) a.Description = x.Description;

			// With the final ids known, the title may name them: a node in an AppTemplate is read once per
			// app, so Title="Log/%APP_ID%" gives every app its own menu entry. Only these two tokens, and
			// only when the id is a concrete one ("*" is a wildcard of a FileRef, not a name).
			a.Title = ExpandTitleIds( a.Title, a.MachineId, a.AppId );

			// add/replace actions
			foreach( var item in x.Actions )
			{
				int found = a.Actions.FindIndex( i => i.Id == item.Id );
				if( found < 0 )
				{
					a.Actions.Add( item );
				}
				else // replace
				{
					a.Actions[found] = item;
				}
			}

		}
		
		static void FillVfsNodeBase( ref VfsNodeDef a, XElement e, string? machineId=null, string? appId=null )
		{
			var assocItemDef = (AssocMenuItemDef)a;
			FillAssocItem( ref assocItemDef, e, machineId, appId );

			var xml = new XElement(e.Name);
			foreach( var attr in e.Attributes() )
				xml.SetAttributeValue(attr.Name, attr.Value);

			var x = new 
			{
				Path = e.Attribute( "Path" )?.Value,
				Filter = e.Attribute( "Filter" )?.Value,
				Children = LoadVfsNodes( e, a.MachineId, a.AppId ),
			};

			if( x.Path is not null ) a.Path = x.Path;
			if( x.Filter is not null ) a.Filter = x.Filter;

			// applies to any node yielding files; not inherited by the children of a container,
			// which are nodes in their own right and may live on another machine altogether
			a.TailBytes = X.getLongAttr( e, "TailBytes", 0 );
			a.Clearable = X.getBoolAttr( e, "Clearable", false );

			a.Xml = xml.ToString();
			a.Children.AddRange( x.Children );

			// add built-in download action
			//a.Actions.Add( new ScriptActionDef() { Name="BuiltIns/DownloadZipped.cs", Title="Download" } );
		}

		static VfsNodeDef? LoadVfsNode( XElement e, string? machineId=null, string? appId=null )
		{
			if (e.Name == "File")
			{
				var a = new FileDef();
				var vfsNode = (VfsNodeDef) a;
				FillVfsNodeBase( ref vfsNode, e, machineId, appId );
				if( string.IsNullOrEmpty(a.Path) ) throw new Exception($"Path missing or empty in {e}");
				if( string.IsNullOrEmpty(a.MachineId) )
				{
					//if( System.IO.Path.IsPathFullyQualified( a.Path ) )
					//	throw new Exception($"Path not fully qualified: {e}");

					var uri = new Uri( a.Path, UriKind.RelativeOrAbsolute );
					if( !uri.IsFile )
						throw new Exception($"Path must be a file: {e}");
					if( !uri.IsUnc )
						throw new Exception($"Path must be UNC: {e}");
				}
				return a;
			}
			else
			if (e.Name == "FileRef")
			{
				var a = new FileRef();
				var vfsNode = (VfsNodeDef) a;
				FillVfsNodeBase( ref vfsNode, e, machineId, appId );
				return a;
			}
			else
			if (e.Name == "Folder")
			{
				var a = new FolderDef();
				var vfsNode = (VfsNodeDef) a;
				FillVfsNodeBase( ref vfsNode, e, machineId, appId );
				a.Mask = e.Attribute( "Mask" )?.Value;
				a.MaxFiles = X.getIntAttr( e, "MaxFiles", 0 );
				a.MaxSeconds = X.getDoubleAttr( e, "MaxSeconds", 0 );
				a.MaxTotalBytes = X.getLongAttr( e, "MaxTotalBytes", 0 );
				a.IsContainer = true;
				return a;
			}
			else
			if (e.Name == "VFolder")
			{
				var a = new VFolderDef();
				var vfsNode = (VfsNodeDef) a;
				FillVfsNodeBase( ref vfsNode, e, machineId, appId );
				a.IsContainer = true;
				return a;
			}
			else
			if (e.Name == "FilePackage")
			{
				var a = new FilePackageDef();
				var vfsNode = (VfsNodeDef) a;
				FillVfsNodeBase( ref vfsNode, e, machineId, appId );
				a.IsContainer = true;
				return a;
			}
			else
			return null;
		}


		static List<VfsNodeDef> LoadVfsNodes( XElement e, string? machineId=null, string? appId=null )
		{
			var res = new List<VfsNodeDef>();
			foreach (var elem in e.Elements())
			{
				var x = LoadVfsNode( elem, machineId, appId );
				if( x is not null )
				{
					res.Add( x );
				}
			}
			return res;

		}


		static void FillActionBase( ref ActionDef a, XElement e, string? machineId=null, string? appId=null )
		{
			var assocItemDef = (AssocMenuItemDef)a;
			FillAssocItem( ref assocItemDef, e, machineId, appId );

			var x = new 
			{
				Name = e.Attribute( "Name" )?.Value,
				Args = e.Attribute( "Args" )?.Value,
				HostId = e.Attribute( "HostId" )?.Value,
				StartupDir = e.Attribute( "StartupDir" )?.Value,
			};

			
			if( x.Name != null ) a.Name = x.Name;
			if( x.Args != null ) a.Args = x.Args;
			if( x.HostId != null ) a.HostId = x.HostId;
			if( x.StartupDir != null ) a.StartupDir = x.StartupDir;
		}

		static void FillToolAction( ref ToolActionDef a, XElement e, string? machineId = null, string? appId = null )
		{
			var act = (ActionDef) a;
			FillActionBase( ref act, e, machineId, appId );
		}

		static void FillScriptAction( ref ScriptActionDef a, XElement e, string? machineId = null, string? appId = null )
		{
			var act = (ActionDef) a;
			FillActionBase( ref act, e, machineId, appId );

			a.AskComment = X.getBoolAttr( e, "AskComment", a.AskComment );
			//var hostId = e.Attribute( "HostId" )?.Value;
			//if (hostId != null) a.HostId = hostId;
		}

		static void FillScriptAction( ref ScriptDef a, XElement e, string? machineId = null, string? appId = null )
		{
			var act = (ActionDef) a;
			FillActionBase( ref act, e, machineId, appId );
			//var hostId = e.Attribute( "HostId" )?.Value;
			//if (hostId != null) a.HostId = hostId;
		}

		public static ActionDef? LoadAction( XElement e, string? machineId=null, string? appId=null )
		{
			if (e.Name == "Tool")
			{
				var a = new ToolActionDef();
				FillToolAction( ref a, e, machineId, appId );
				return a;
			}
			else
			if (e.Name == "Script")
			{
				var a = new ScriptActionDef();
				FillScriptAction( ref a, e, machineId, appId );
				return a;
			}
			else
			return null;
		}

		static List<ActionDef> LoadActions( XElement e, string? machineId=null, string? appId=null )
		{
			var res = new List<ActionDef>();
			foreach (var elem in e.Elements())
			{
				var x = LoadAction( elem, machineId, appId );
				if( x is not null )
				{
					res.Add( x );
				}
			}
			return res;

		}


		// the single-instance scripts identified by a GUID Id
		static ScriptDef LoadSingleInstScript( XElement e )
		{
			var a = new ScriptDef();
			FillScriptAction( ref a, e );

			// we read the Guid Id attribute
			if( string.IsNullOrEmpty(a.Id) )
				throw new ConfigurationErrorException( $"Id missing in {e}" );

			if( !Guid.TryParse( a.Id, out a.Guid ) )
				throw new ConfigurationErrorException( $"Id must be a GUID in {e}" );

			if ( string.IsNullOrEmpty(a.Name) )
				throw new ConfigurationErrorException( $"Missing Name in {e}");

			return a;
		}
					
		static FileShareDef LoadFileShare( XElement e )
		{
			FileShareDef a = new FileShareDef();

			var x = new 
			{
				Name = e.Attribute( "Name" )?.Value,
				Path = e.Attribute( "Path" )?.Value,
			};

			if( x.Name == null )
				throw new Exception( $"File share definition missing the Name attribute: {e}" );

			if( x.Path == null )
				throw new Exception( $"File share definition missing the Path attribute: {e}" );

			a.Name = x.Name;
			a.Path = x.Path;

			return a;
		}

		void LoadAppDefaults()
		{
			_cfg.AppDefaults.Clear();

			var apps = from e in _root.Elements( "App" )
						select e;

			foreach( var p in apps )
			{
				var app = ReadAppElement( p, _root, _fdReg, null, null );
				_cfg.AppDefaults.Add( app );
			}
		}

		void LoadPlans()
		{
			var plans = from e in _root.Elements( "Plan" )
						select e;

			int planIndex = 0;
			foreach( var p in plans )
			{
				planIndex++;
				var planName = p.Attribute( "Name" )?.Value;
				var startTimeout = X.getDoubleAttr( p, "StartTimeout", -1, true );

				var apps = ( from e in p.Elements( "App" )
							 select ReadAppElement( e, _root, _fdReg, null, null ) ).ToList();

				if( string.IsNullOrEmpty(planName) )
					throw new ConfigurationErrorException( $"Missing plan name in plan #{planIndex}");

				var groups = p.Attribute( "Groups" )?.Value ?? string.Empty;

				var applyOnStart = X.getBoolAttr( p, "ApplyOnStart", false, true );

				var applyOnSelect = X.getBoolAttr( p, "ApplyOnSelect", false, true );

				// check if everything is valid
				int index = 1;
				foreach( var a in apps )
				{
					a.PlanName = planName;

					if( string.IsNullOrEmpty(a.Id.AppId) || string.IsNullOrEmpty(a.Id.MachineId) )
					{
						throw new ConfigurationErrorException( string.Format( "App #{0} in plan '{1}' not having valid AppTupleId", index, planName ) );
					}

					if( a.ExeFullPath == null )
					{
						throw new ConfigurationErrorException( string.Format( "App #{0} in plan '{1}' not having valid ExeFullPath", index, planName ) );
					}

					index ++;
				}

				_cfg.Plans.Add(
					new PlanDef()
					{
						Name = planName,
						AppDefs = apps,
						StartTimeout = startTimeout,
						Groups = groups,
						ApplyOnStart = applyOnStart,
						ApplyOnSelect = applyOnSelect
					}
				);
			}

		}

		bool AppExists( AppIdTuple id )
		{
			foreach( var pd in _cfg.Plans )
			{
				foreach( var ad in pd.AppDefs )
				{
					if( ad.Id == id )
						return true;
				}
			}

			foreach( var ad in _cfg.AppDefaults )
			{
				if( ad.Id == id )
					return true;
			}

			return false;
		}

		void CheckDependenciesExist( string source, IEnumerable<AppDef> appDefs )
		{
			foreach( var ad in appDefs )
			{
				if( ad.Dependencies is not null )
				{
					foreach( var depName in ad.Dependencies )
					{
						// a dependency without a machine means one on the depending app's own machine, which
						// is how AppLaunchPlanner reads it at run time; parsing it any other way here would
						// reject a config that then works
						var depId = AppIdTuple.fromString( depName, ad.Id.MachineId );
						if( !AppExists( depId ) )
						{
		                    throw new UnknownDependencyException( $"{source}: {ad.Id}: Dependency {depName} not found." );
						}
					}
				}
			}
		}

		/// <summary>
		/// Refuses a plan whose dependencies lead back to where they started.
		/// Checks within a single plan only - it cannot tell whether a cross-plan cycle is a problem.
		/// </summary>
		/// <param name="onPath">
		/// The applications between the start of this walk and here. A dependency that is already on
		/// the path is a cycle; one merely *visited before* is not, which is the distinction this used
		/// to get wrong - see the remarks.
		/// </param>
		/// <param name="chain">the same, in order, so the message can show the loop</param>
		/// <param name="proven">applications already walked and found acyclic; shared across one plan</param>
		/// <remarks>
		/// This used to keep one set of "dependencies used" for the whole walk and never take anything
		/// out of it when a branch finished, so it fired on any application reachable by two routes at
		/// once. A plan where two applications wait for the same third one - a database, a licence
		/// server, a recorder - was refused as circular and the master would not load the config at
		/// all. Reported from a live site, and pinned by DependencyCheckTests.
		///
		/// Marking what has been proven acyclic is what keeps this linear: without it a diamond is
		/// walked once per route into it, and a chain of diamonds costs two to the power of its length.
		/// </remarks>
		void CheckDependenciesCircular( PlanDef planDef, AppDef ad,
				HashSet<AppIdTuple> onPath, List<AppIdTuple> chain, HashSet<AppIdTuple> proven )
		{
			if( proven.Contains( ad.Id ) ) return; // walked already, and no cycle came of it

			onPath.Add( ad.Id );
			chain.Add( ad.Id );

			if( ad.Dependencies is not null )
			{
				foreach( var depName in ad.Dependencies )
				{
					// a bare name means the depending app's own machine. Parsed without that default,
					// the recursion below never found the dependency and a cycle written with bare
					// names went undetected.
					var depId = AppIdTuple.fromString( depName, ad.Id.MachineId );

					if( onPath.Contains( depId ) )
					{
						throw new CircularDependencyException(
								$"{planDef.Name}: {ad.Id}: Circular dependency {depName} found."
								+ $" ({string.Join( " -> ", chain )} -> {depId})" );
					}

					// check it recursively
					var depAppDef = planDef.AppDefs.FirstOrDefault( x => x.Id == depId );
					if( depAppDef is not null )
					{
						CheckDependenciesCircular( planDef, depAppDef, onPath, chain, proven );
					}
				}
			}

			chain.RemoveAt( chain.Count - 1 );
			onPath.Remove( ad.Id );
			proven.Add( ad.Id );
		}

		void CheckDependencies()
		{
			CheckDependenciesExist( $"AppDefaults", _cfg.AppDefaults );

			// check if all dependencies mentioned exists either in a plan or in app defaults
			foreach( var pd in _cfg.Plans )
			{
				CheckDependenciesExist( $"Plan {pd.Name}", pd.AppDefs );
			}

			// check circular dependency within a plan
			// WARNING: does not find cross-plan circular dependencies.. not possible to tell if such dep is a real problem or not
			foreach( var pd in _cfg.Plans )
			{
				// what has been proven acyclic carries over between the starting points: every
				// application of the plan is then walked once, however the dependencies fan in
				var proven = new HashSet<AppIdTuple>();

				foreach( var ad in pd.AppDefs )
				{
					CheckDependenciesCircular( pd, ad, new HashSet<AppIdTuple>(), new List<AppIdTuple>(), proven );
				}
			}

		}

		static List<ScriptDef> LoadSingleInstScripts( XElement root )
		{
			var res = new List<ScriptDef>();

			var elems = from e in root.Elements( "Script" )
						select e;
			foreach( var e in elems )
			{
				var def = LoadSingleInstScript( e );
				res.Add(def);
			}

			return res;
		}


		static ActionDef? LoadToolAction( XElement e )
		{
			if (e.Name == "Script")
			{
				var a = new ScriptActionDef();
				FillScriptAction( ref a, e );
				return a;
			}
			else
			if (e.Name == "Tool")
			{
				var a = new ToolActionDef();
				FillToolAction( ref a, e );
				return a;
			}
			else
			return null;
		}

		static List<AssocMenuItemDef> LoadMainMenuItems( XElement root )
		{
			var res = new List<AssocMenuItemDef>();

			foreach( var menuRoot in root.Elements("MainMenu") )
			{
				foreach( var elem in menuRoot.Elements() )
				{
					var action = LoadToolAction( elem );
					if( action is not null )
					{
						res.Add( action );
						continue;
					}

					var vfsNode = LoadVfsNode( elem );
					if( vfsNode is not null )
					{
						res.Add( vfsNode );
					}
				}
			}
			return res;
		}


		static List<FileShareDef> LoadShares( XElement root )
		{
			var res = new List<FileShareDef>();

			var elems = from e in root.Elements( "Share" )
						select e;
			foreach( var e in elems )
			{
				var def = LoadFileShare( e );
				res.Add(def);
			}

			return res;
		}

		void LoadMachines( FileDefReg fdReg )
		{
			_cfg.Machines.Clear();

			var machines = from e in _root.Elements( "Machine" )
						select e;

			int index = 0;
			foreach( var p in machines )
			{
				index++;
				var id = X.getStringAttr( p, "Name", "" );
				var ip = X.getStringAttr( p, "IP", "" );
				var MAC = X.getStringAttr( p, "MAC", "" );
				var shares = LoadShares( p );

				if ( string.IsNullOrEmpty(id) )
					throw new ConfigurationErrorException( $"Missing machine name in {p} #{index}");

				var vfsNodes = LoadVfsNodes( p, id, null ).Select( n => fdReg.Add( n ) ).ToList();
				
				_cfg.Machines.Add(
					new MachineDef()
					{
						Id = id,
						IP = ip,
						MAC = MAC,
						FileShares = shares,
						VfsNodes = vfsNodes,
						Actions = LoadActions( p, id, null ),
					}
				);;
			}

		}

		void LoadUnboundFiles( FileDefReg fdReg )
		{
			// just feed them to the registry
			foreach ( var vfsNode in LoadVfsNodes( _root, null, null ) )
			{
				fdReg.Add( vfsNode );
			}
		}

		void LoadKillAllExtras()
		{
			_cfg.KillAllExtras.Clear();

			var killAllElem = _root.Element( "KillAll" );
			if( killAllElem is null ) return;

			foreach( var e in killAllElem.Elements() )
			{
				var action = LoadToolAction( e );
				if( action is not null )
				{
					if( string.IsNullOrEmpty(action.HostId) )
						throw new ConfigurationErrorException( $"Missing HostId in {e}");	
					_cfg.KillAllExtras.Add( action );
				}
			}
		}



	}
}
