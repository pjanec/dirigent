
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System;
using System.IO;
using System.Xml.Linq;
using System.Threading;
using System.IO.Enumeration;
using X = Dirigent.XmlConfigReaderUtils;

namespace Dirigent
{

	/// <summary>
	/// List of registered files and packages
	/// </summary>
	public class FileRegistry
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public delegate string? GetMachineIPDelegate( string machineId );

		public GetMachineIPDelegate _machineIPDelegate;

		string _rootForRelativePaths;

		string? _downloadFolder;

		public class TMachine
		{
			public string Id = string.Empty;
			public string? IP = string.Empty;  // will be replaced with real IP once found
			public Dictionary<string, string> Shares = new Dictionary<string, string>();
		}

		public List<FilePackageDef> PackageDefs { get; private set; } = new List<FilePackageDef>();

		// all VfsNodes found when traversing SharedDefs
		public Dictionary<Guid, VfsNodeDef> VfsNodes { get; private set; } = new Dictionary<Guid, VfsNodeDef>();
		
		public Dictionary<string, TMachine> Machines { get; private set; } = new Dictionary<string, TMachine>();

		private string _localMachineId = string.Empty;

		IDirig _ctrl;
		
		/// <param name="downloadFolder">
		/// What %DOWNLOADS% expands to on this machine. Empty or null means the download folder of
		/// the user this process runs as.
		/// </param>
		public FileRegistry( IDirig ctrl, string localMachineId, string rootForRelativePaths, GetMachineIPDelegate machineIdDelegate, string? downloadFolder = null )
		{
			_ctrl = ctrl;
			_localMachineId = localMachineId;
			_rootForRelativePaths = rootForRelativePaths;
			_downloadFolder = downloadFolder;
			_machineIPDelegate = machineIdDelegate;
		}
		
		public VfsNodeDef? GetVfsNodeDef( Guid guid )
		{
			if( VfsNodes.TryGetValue( guid, out var def ) ) return def;
			return null;
		}

		public IEnumerable<VfsNodeDef> GetAllVfsNodesDef() => VfsNodes.Values;


		public void SetVfsNodes( IEnumerable<VfsNodeDef> vfsNodes )
		{
			VfsNodes = vfsNodes.ToDictionary( n => n.Guid );
		}


		public void Clear()
		{
			Machines.Clear();
			VfsNodes.Clear();
		}

		public void SetMachines( IEnumerable<MachineDef> machines )
		{
			Machines.Clear();
			foreach( var mdef in machines )
			{
				var m = new TMachine();

				m.Id = mdef.Id;
				m.IP = mdef.IP;

				foreach( var s in mdef.FileShares )
				{
					if( !PathUtils.IsPathAbsolute(s.Path)  )
						throw new Exception($"Share part not absolute: {s}");

					m.Shares[s.Name] = s.Path;
				}

				Machines[mdef.Id] = m;
			}
		}

		public string GetMachineIP( string machineId )
		{
			string? ip = null;

			// find machine
			if( Machines.TryGetValue( machineId, out var m ) )
				ip = m.IP;
				
				// find machine IP
			if( string.IsNullOrEmpty( ip ) )
			{
				if( _machineIPDelegate != null && !string.IsNullOrEmpty( machineId ) )
				{
					ip = _machineIPDelegate( machineId );
				}
			}

			if( string.IsNullOrEmpty( ip ) )
				throw new Exception($"Could not find IP of machine {machineId}.");

			// remember the machine if not yet
			if( m is null )
			{
				m = new TMachine(); 
			}

			if( string.IsNullOrEmpty(m.IP) )
			{
				m.IP = ip;
			}

			return m.IP;
		}

		/// <summary>
		/// Turns a path local to the given machine into a UNC path leading through one of that
		/// machine's file shares.
		/// </summary>
		/// <remarks>
		/// The share covering the path most specifically wins, the way a mount table works, so that
		/// a share dedicated to a subtree (D:\Logs) is preferred over one covering the whole drive
		/// (D:\). Without that the winner would depend on the order the shares happen to be stored
		/// in, and a share declared for a particular folder - typically the one with the permissions
		/// set up for it - could be bypassed.
		/// </remarks>
		public string MakeUNC( string path, string? machineId, string whatFor )
		{
			// global paths are already UNC
			if ( string.IsNullOrEmpty(machineId) )
				return path;

			// find machine
			if ( !Machines.TryGetValue( machineId, out var m ) )
				throw new Exception($"Machine {machineId} not found for {whatFor}");

			var IP = GetMachineIP( machineId );

			string? bestName = null;
			string? bestRoot = null;

			foreach( var (shName, shPath) in m.Shares )
			{
				var root = ShareRootCoveringPath( shPath, path );
				if( root is null )
					continue;

				if( bestRoot is null
					|| root.Length > bestRoot.Length
					// two shares of the same folder: pick by name, just to stay predictable
					|| ( root.Length == bestRoot.Length && string.CompareOrdinal( shName, bestName ) < 0 ) )
				{
					bestName = shName;
					bestRoot = root;
				}
			}

			if( bestRoot is null )
				throw new Exception($"Can't construct UNC path, No file share matching {whatFor}");

			var pathRelativeToShare = path.Substring( bestRoot.Length ).TrimStart( '\\', '/' );

			return string.IsNullOrEmpty( pathRelativeToShare )
					? $"\\\\{IP}\\{bestName}"
					: $"\\\\{IP}\\{bestName}\\{pathRelativeToShare}";
		}

		/// <summary>
		/// The share's folder, without a trailing separator, if the share contains the given path;
		/// null if it does not.
		/// </summary>
		/// <remarks>
		/// The path must continue at a folder boundary, otherwise a share at "D:\Logs" would claim
		/// "D:\LogsBackup\a.txt" and silently produce a UNC path to a different file.
		/// </remarks>
		static string? ShareRootCoveringPath( string sharePath, string path )
		{
			var shareRoot = sharePath.TrimEnd( '\\', '/' );

			if( !path.StartsWith( shareRoot, StringComparison.OrdinalIgnoreCase ) )
				return null;

			if( path.Length == shareRoot.Length ) // the share's own folder
				return shareRoot;

			var next = path[shareRoot.Length];
			return ( next == '\\' || next == '/' ) ? shareRoot : null;
		}
		
		public string MakeUNCIfNotLocal( string path, string? machineId, string whatFor )
		{
			if( _localMachineId != machineId )
			{
				return MakeUNC( path, machineId, whatFor );
			}
			else
			{
				return path;
			}
		}

		/// <summary>
		/// Returns direct path to the file, with all variables and file path resolution mechanism already evaluated.
		/// If we are on the machine where the file is, returns local path, otherwise returns remote path.
		/// </summary>
		/// <param name="fdef"></param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		string? ResolveFilePath( VfsNodeDef fdef, bool forceUNC )
		{
			// global file? must be UNC path already...
			if( string.IsNullOrEmpty( fdef.MachineId ) )
			{
				if( string.IsNullOrEmpty( fdef.Path ) )
				{
					throw new Exception($"FileDef path empty: {fdef}");
				}

				return fdef.Path;
			}

			if( string.IsNullOrEmpty( fdef.Path ) )
			{
				throw new Exception($"FileDef path empty: {fdef}");
			}

			bool isLocal = IsLocalMachine(fdef.MachineId);

			var path = fdef.Path;

			// expand variables in local context
			if( isLocal )
			{
				var vars = new Dictionary<string, string>();

				// the download folder of this machine; configurable so that it need not be the
				// real user folder (a test run must not litter it)
				vars["DOWNLOADS"] = string.IsNullOrEmpty( _downloadFolder )
										? Tools.GetDownloadFolderPath()
										: _downloadFolder;

				// for app-bound files, expand also local vars and define var for app working dir etc.
				if( fdef.MachineId == _localMachineId ) // are we the agent for this machine?
				{
					// KEEP IN SYNC WITH Launcher.cs
					vars["MACHINE_ID"] = _localMachineId;
					vars["DIRIGENT_MACHINE_ID"] = _localMachineId;

					// The IP is only known once the machine definitions have arrived, and asking
					// for it throws when they have not. Resolving a path that does not mention the
					// IP at all must not fail for that reason, so only look it up when it is used.
					if( MentionsMachineIP( path ) )
					{
						var machineIP = GetMachineIP( _localMachineId );
						vars["MACHINE_IP"] = machineIP;
						vars["DIRIGENT_MACHINE_IP"] = machineIP;
					}
				
					if( !string.IsNullOrEmpty( fdef.AppId ) )
					{
						var appDef = _ctrl.GetAppDef( new AppIdTuple( fdef.MachineId, fdef.AppId ) );
						if( appDef is not null )
						{
							foreach( var (k,v) in appDef.EnvVarsToSet )
								vars[k] = v;

							// add some app-special vars
							vars["DIRIGENT_APPID"] = appDef.Id.AppId;
							vars["APP_ID"] = appDef.Id.AppId;
							vars["APP_BINDIR"] = Tools.ExpandEnvAndInternalVars( Path.GetDirectoryName(appDef.ExeFullPath)!, appDef.EnvVarsToSet );
							vars["APP_STARTUPDIR"] = Tools.ExpandEnvAndInternalVars( appDef.StartupDir, appDef.EnvVarsToSet );
						}
					}
				}

				path = Tools.ExpandEnvAndInternalVars( path, vars );

				if( !PathUtils.IsPathAbsolute( path ) )
				{
					path = Path.Combine( _rootForRelativePaths, path );
				}
			}

			// if the file on local machine, return local path
			if( isLocal && !forceUNC )
			{
				return path;
			}


			// construct UNC path using file shares defined for machine

			var machineId = isLocal ? _localMachineId : fdef.MachineId;

			return MakeUNC( path, machineId, $"FileDef {fdef}" );
		}

		/// <summary>
		/// Whether the path uses the machine IP variable, in any of its spellings.
		/// </summary>
		static bool MentionsMachineIP( string path )
			=> path.IndexOf( "MACHINE_IP", StringComparison.OrdinalIgnoreCase ) >= 0;

		bool IsMatch( string? pattern, string? str )
		{
			if( string.IsNullOrEmpty( pattern ) ) // empty pattern means that anything matches
				return true;

			if( str is null ) // null string only matches if the pattern allows anything
				return pattern == "*";

			return FileSystemName.MatchesSimpleExpression( pattern, str );

			// wildcard pattern allowing single asterisk at the end
			//if (pattern.EndsWith("*") )
			//{
			//	string beforeAsterisk = pattern.Substring(0, pattern.Length-1);
			//	return str.StartsWith( beforeAsterisk, StringComparison.OrdinalIgnoreCase );
			//}

			//return string.Equals(str, pattern, StringComparison.OrdinalIgnoreCase);
		}

		List<VfsNodeDef> FindById( string Id, string? machineId, string? appId )
		{
			var res = new List<VfsNodeDef>();
			foreach( var node in VfsNodes.Values )
			{
				// empty string equals to null; this allows nullifying the machine/app inherited from parent node in shared config by using empty string
				if( !IsMatch( Id, node.Id ) )
					continue;
					
				if( !IsMatch( machineId, node.MachineId ) )
					continue;

				if( !IsMatch( appId, node.AppId ) )
					continue;

				// match!
				res.Add( node );
			}
			return res;
		}

		static T EmptyFrom<T>( VfsNodeDef x ) where T: VfsNodeDef, new()
		{
			var r = new T();
			//r.Guid = x.Guid; // gud should stay unique
			r.Id = x.Id;
			r.Title = x.Title;
			r.MachineId = x.MachineId;
			r.AppId = x.AppId; // kept so that the actions can tell which app the resolved file belongs to
			r.TailBytes = x.TailBytes; // the download needs it, and only the definition knows it
			r.Clearable = x.Clearable; // ditto - Clear and Mark act on the resolved node
			return r;
		}

		bool IsLocalMachine( string clientId )
		{
			if( clientId == _localMachineId )
				return true;

			// try compare IP addresses
			var clientIP = _machineIPDelegate( clientId );
			if( clientIP is null )
				return false;

			var ourIP = _machineIPDelegate( _localMachineId );
			if( ourIP is null )
				return false;

			if( clientIP == ourIP )
				return true;

			return false;
		}

		/// <summary>
		/// Converts given VfsNode into a tree of virtual folders containing links to physical files.
		/// Resolves all links, scans the folders (remembering the contained files and subfolders if requested), expands variables.
		/// File paths returned are resolved from the perspective of the local machine - remote paths are UNC, variables expanded to values found on the remote machines.
		/// </summary>
		/// <param name="def">Root node of what to resolve</param>
		/// <param name="forceUNC">If true, all paths will be UNC, even if they are on the local machine</param>
		/// <param name="includeContent">If true, will include content of folders, otherwise will just include the folders themselves</param>
		/// <returns>
		///  Folders - VFolder will have just the Title (vfolder name).
		/// Files will have the Title and Path (link to physical file).
		/// </returns>
		public async Task<VfsNodeDef?> ResolveAsync( IDirigAsync iDirig, VfsNodeDef nodeDef, bool forceUNC, bool includeContent, List<Guid>? usedGuids )
		{
			if (nodeDef is null)
				throw new ArgumentNullException( nameof( nodeDef ) );

			var pending = new List<RemoteStub>();

			// everything this machine can work out on its own, with a stub left standing wherever
			// another machine has to be asked
			var root = ResolveLocally( nodeDef, forceUNC, includeContent, usedGuids, pending, null );

			if( pending.Count > 0 )
			{
				await AskTheMachines( iDirig, pending, forceUNC );

				foreach( var stub in pending )
					root = PutInPlace( stub, root );
			}

			return root;
		}

		/// <summary>
		/// A node of another machine, standing in the tree until that machine has answered about it.
		/// </summary>
		/// <remarks>
		/// The walk happens here, on this machine, and stops at every node belonging to somebody
		/// else - what a folder on another machine holds is not knowable from here. Each of those
		/// used to be a round trip of its own, taken one after another, so a package of thirty nodes
		/// cost thirty waits however few machines it spanned. Now the walk leaves one of these behind
		/// and carries on, and when it is done every machine is asked once, about all of its own
		/// nodes, and all the machines at the same time.
		///
		/// A stub never leaves this class: by the time the tree is returned every one of them has
		/// been replaced by what its machine said, or taken out.
		/// </remarks>
		class RemoteStub : VfsNodeDef
		{
			/// <summary>The node, to be resolved by the machine it belongs to.</summary>
			public VfsNodeDef Request = null!;

			/// <summary>The container it stands in; null means it is the root of this resolution.</summary>
			public VfsNodeDef? Parent;

			/// <summary>
			/// What takes its place if the machine cannot answer, and what then carries the note.
			/// Null means nothing does - the node is dropped and the container is told why.
			/// </summary>
			public VfsNodeDef? Fallback;

			/// <summary>
			/// Whether the machine should look inside the node. Kept per stub because a container's
			/// children are always asked for with their content, whatever was asked of the container.
			/// </summary>
			public bool IncludeContent;

			public VfsNodeDef? Resolved;
			public Exception? Failure;
		}

		/// <summary>
		/// The part of the resolution this machine can do on its own, leaving a <see cref="RemoteStub"/>
		/// wherever another machine has to be asked.
		/// </summary>
		/// <param name="parent">the container the result will be put into; null for the root</param>
		VfsNodeDef? ResolveLocally( VfsNodeDef nodeDef, bool forceUNC, bool includeContent,
				List<Guid>? usedGuids, List<RemoteStub> pending, VfsNodeDef? parent )
		{
			if (usedGuids == null) usedGuids = new List<Guid>();
			if (usedGuids.Contains( nodeDef.Guid ))
			{
				//	throw new Exception( $"Circular reference in VFS tree: {nodeDef}" );
				return null;
			}

			usedGuids.Add( nodeDef.Guid );

			// non-local stuff to be always resolved on machine where local - via remote script call
			// (a FileRef is the exception: it is resolved here, by looking it up in the registry we
			// hold a copy of. Its machine id is a filter for that lookup and may be a wildcard, so
			// it is not the name of a machine to send the reference to. Whatever the lookup finds
			// then gets resolved on its own machine.)
			if( nodeDef is not FileRef
				&& !string.IsNullOrEmpty(nodeDef.MachineId) // global resources are machine independent - can be resolved on any machine
				&& !IsLocalMachine(nodeDef.MachineId) )
			{
				// check if required machine is available
				if( !string.IsNullOrEmpty(nodeDef.MachineId) &&  _machineIPDelegate( nodeDef.MachineId ) is null )
					throw new Exception($"Machine {nodeDef.MachineId} not connected.");

				var stub = new RemoteStub()
				{
					Request = nodeDef,
					Parent = parent,
					IncludeContent = includeContent,
				};

				pending.Add( stub );
				return stub;
			}

			// from here on, we are on local machine (or master)

			if( nodeDef is FileDef fileDef )
			{
				return ResolveFileDef( forceUNC, fileDef );
			}
			else
			if( nodeDef is FileRef fref )
			{
				return ResolveFileRef( forceUNC, includeContent, usedGuids, fref, pending, parent );
			}
			else
			if (nodeDef is VFolderDef vfolderDef)
			{
				return ResolveVFolder( vfolderDef, forceUNC, usedGuids, pending );
			}
			else
			if( nodeDef is FolderDef folderDef )
			{
				return ResolveFolder( folderDef, forceUNC, includeContent );
			}
			else
			if( nodeDef is FilePackageDef fpdef )
			{
				return ResolveVFolder( fpdef, forceUNC, usedGuids, pending );
			}
			else
			{
				throw new Exception( $"Unknown VfsNodeDef type: {nodeDef}" );
			}
		}

		/// <summary>
		/// Asks every machine about all of its own nodes in one call, and all the machines at once.
		/// </summary>
		/// <remarks>
		/// This is where the time of a collection on a large site is spent, so it is worth being
		/// precise about what it now costs: one round trip, however many machines and however many
		/// nodes - where it used to be one round trip per node, in sequence.
		/// </remarks>
		async Task AskTheMachines( IDirigAsync iDirig, List<RemoteStub> pending, bool forceUNC )
		{
			// Grouped in the order the machines first appear, so that what goes on the wire is as
			// predictable as what the walk produced. Nodes wanted without their content travel
			// separately - it is one flag for the whole call.
			var byMachine = new List<List<RemoteStub>>();
			var whichGroup = new Dictionary<string, int>();

			foreach( var stub in pending )
			{
				var key = $"{stub.Request.MachineId}|{stub.IncludeContent}";

				if( !whichGroup.TryGetValue( key, out var at ) )
				{
					at = byMachine.Count;
					whichGroup[key] = at;
					byMachine.Add( new List<RemoteStub>() );
				}

				byMachine[at].Add( stub );
			}

			log.Debug( $"Resolving {pending.Count} node(s) in {byMachine.Count} call(s)" );

			await Task.WhenAll( byMachine.Select( group => AskOneMachine( iDirig, group, forceUNC ) ) );
		}

		async Task AskOneMachine( IDirigAsync iDirig, List<RemoteStub> stubs, bool forceUNC )
		{
			var machineId = stubs[0].Request.MachineId ?? string.Empty;

			try
			{
				var args = new Scripts.BuiltIn.ResolveVfsPath.TArgs
				{
					VfsNodes = stubs.Select( x => x.Request ).ToList(),
					ForceUNC = forceUNC,
					IncludeContent = stubs[0].IncludeContent
				};

				var title = stubs.Count == 1
						? $"Resolve {stubs[0].Request.Xml}"
						: $"Resolve {stubs.Count} nodes of {machineId}";

				var result = await iDirig.RunScriptAsync<Scripts.BuiltIn.ResolveVfsPath.TArgs, Scripts.BuiltIn.ResolveVfsPath.TResult>(
						machineId,
						Scripts.BuiltIn.ResolveVfsPath._Name,
						"",	// sourceCode
						args,
						title,
						out var instance
					);

				var answers = result?.Nodes;
				if( answers is null || answers.Count != stubs.Count )
					throw new Exception( $"{machineId} answered about {answers?.Count.ToString() ?? "none"}"
							+ $" of the {stubs.Count} node(s) it was asked about." );

				for( int i = 0; i < stubs.Count; i++ )
				{
					if( answers[i].Error is string error )
						stubs[i].Failure = new Exception( error );
					else
						stubs[i].Resolved = answers[i].VfsNode;
				}
			}
			catch( Exception ex )
			{
				// the machine could not be asked at all, or did not answer sensibly - which is every
				// one of its nodes failing, exactly as each would have failed on its own
				foreach( var stub in stubs )
					stub.Failure ??= ex;
			}
		}

		/// <summary>
		/// Puts what a machine answered where the stub was standing, or takes the stub out and says
		/// why. Returns the root of the tree, which is a different node when the stub was the root.
		/// </summary>
		VfsNodeDef? PutInPlace( RemoteStub stub, VfsNodeDef? root )
		{
			VfsNodeDef? replacement;

			if( stub.Failure is not null )
			{
				// a node asked for on its own leaves nothing else to deliver, so it fails out loud,
				// just as it did when it had a call of its own - see ResolveChild
				if( stub.Parent is null && stub.Fallback is null )
					System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture( stub.Failure ).Throw();

				log.Warn( $"Could not resolve {stub.Request}: {stub.Failure.Message}" );

				var target = stub.Fallback ?? stub.Parent!;
				( target.Notes ??= new List<string>() ).Add( CouldNotLookUp( stub.Request, stub.Failure ) );

				replacement = stub.Fallback;
			}
			else
			{
				replacement = stub.Resolved ?? stub.Fallback;
			}

			if( stub.Parent is null )
				return ReferenceEquals( root, stub ) ? replacement : root;

			// by reference: two nodes of a tree can be equal by value without being the same node
			var at = stub.Parent.Children.FindIndex( x => ReferenceEquals( x, stub ) );
			if( at < 0 )
				return root;

			if( replacement is null )
				stub.Parent.Children.RemoveAt( at );
			else
				stub.Parent.Children[at] = replacement;

			return root;
		}

		private VfsNodeDef? ResolveFileRef( bool forceUNC, bool includeContent, List<Guid>? usedGuids,
				FileRef fref, List<RemoteStub> pending, VfsNodeDef? parent )
		{
			var defs = FindById( fref.Id, fref.MachineId, fref.AppId );

			// remove reference to self
			defs.RemoveAll( x => x.Guid == fref.Guid );

			if( defs.Count == 0 )
				return null;

			if ( defs.Count == 1 )
			{
				// Guarded like the many-node case below, and for the same reason - but the note has to
				// name the node that failed rather than the reference that found it: a reference is
				// written with wildcards ("every 'dump' node, on any machine"), and "'dump' on *"
				// tells nobody which machine is missing the folder.
				var single = EmptyFrom<VFolderDef>( fref );
				single.IsContainer = true;

				var resolvedSingle = ResolveChild( defs[0], forceUNC, usedGuids, pending,
						parent: parent, fallback: single );

				// the pack exists only to carry a note; when there is none, the node stands alone as
				// it always did
				return resolvedSingle ?? single;
			}

			{
				var pack = new VFolderDef();
				pack.Title = fref.Title;
				if ( string.IsNullOrEmpty(pack.Title) ) pack.Title = fref.Id;
				if( string.IsNullOrEmpty(pack.Title) ) pack.Title = fref.Guid.ToString();

				// guarded one by one, like a container's children: a reference matching thirty nodes
				// across two machines must not be lost to one of them - see ResolveChild
				foreach( var def in defs )
				{
					var resolved = ResolveChild( def, forceUNC, usedGuids, pending, parent: pack );
					if( resolved is not null )
						pack.Children.Add( resolved );
				}
				return pack;
			}
				
			
		}

		private VfsNodeDef? ResolveFileDef( bool forceUNC, FileDef fileDef )
		{
			if (string.IsNullOrEmpty( fileDef.Path )) throw new Exception( $"FileDef.Path is empty. {fileDef.Xml}" );


			//if( fileDef.Path.Contains('%') )

			if (string.IsNullOrEmpty( fileDef.Filter ))
			{
				var r = EmptyFrom<ResolvedVfsNodeDef>( fileDef );
				r.IsContainer = false;
				r.Guid = fileDef.Guid;
				r.Path = ResolveFilePath( fileDef, forceUNC );
				if( r.Path is null ) return null;
				return r;
			}

			// newest file(s) from folder?
			//  - if just one file is requested, return one single FileDef or null
			//  - if multiple files allowed, return VFolder
			if (fileDef.Filter.Equals( "newest", StringComparison.OrdinalIgnoreCase ))
			{
				var folder = ResolveFilePath( fileDef, forceUNC );
				if( folder is null )
					return null;

				if (string.IsNullOrEmpty( fileDef.Xml )) throw new Exception( $"FileDef.Xml is empty. {fileDef.Xml}" );
				var xml = XElement.Parse( fileDef.Xml );

				string mask = X.getStringAttr( xml, "Mask", "*.*" );
				int maxFiles = X.getIntAttr( xml, "MaxFiles", 1 ); // by default a single file only
				if (maxFiles < 1) maxFiles = 1;
				double maxSeconds = X.getDoubleAttr( xml, "MaxSeconds", double.MaxValue ); // by default whatever age

				var newestFiles = GetNewestFilesInFolder( folder, mask, maxFiles, maxSeconds );

				// if just one single file requested, return FileDef
				if( maxFiles <= 1 )
				{
					if( newestFiles.Count == 0 )
					{
						return null;
					}
					else
					{
						var r = EmptyFrom<FileDef>( fileDef );
						r.Guid = fileDef.Guid;
						r.Path = newestFiles[0];
						return r;
					}
				}
				else
				// if more files possible, put them in VFolder
				{
					var pack = EmptyFrom<VFolderDef>( fileDef );
					if( string.IsNullOrEmpty(pack.Title) ) pack.Title = pack.Id;
					if( string.IsNullOrEmpty(pack.Title) ) pack.Title = pack.Guid.ToString();
					foreach( var fpath in newestFiles )
					{
						var r = EmptyFrom<FileDef>( fileDef );
						r.Guid = fileDef.Guid;
						r.Path = fpath;
						pack.Children.Add( r );
					}
					return pack;
				}
			}

			throw new Exception( $"Unsupported filter. {fileDef.Xml}" );
		}

		VfsNodeDef ResolveVFolder( VfsNodeDef folderDef, bool forceUNC, List<Guid>? usedGuids, List<RemoteStub> pending )
		{
			var rootNode = EmptyFrom<ResolvedVfsNodeDef>( folderDef ); // this produces Iscontainer=false (ResolvedVfsNode does not say if it is a container or not)
			rootNode.IsContainer = true;

			// children of other machines become stubs here and are asked for together afterwards,
			// one call per machine - see RemoteStub
			foreach ( var child in folderDef.Children )
			{
				var resolved = ResolveChild( child, forceUNC, usedGuids, pending, parent: rootNode );
				if( resolved is not null )
				{
					rootNode.Children.Add( resolved );
				}
			}

			return rootNode;
		}

		/// <summary>
		/// Resolves one member of a container, turning a failure into a note on the container rather
		/// than into a failure of the whole thing.
		/// </summary>
		/// <remarks>
		/// A package is a list of things the operator asked for, and the things are independent: a
		/// folder that is not on this machine, a machine whose shares are unknown, an application that
		/// has never crashed and therefore has no CrashDumps folder. Before this, any one of those
		/// aborted the resolution of the whole package - so a system-wide collection could be lost to
		/// a folder that has never existed on one machine, which is at its most likely just after an
		/// incident, when the collection matters most.
		///
		/// The note travels with the tree into the archive's _incomplete.txt, so that an archive which
		/// lacks something says so. Resolving a node on its own still fails out loud: there the caller
		/// asked for that one thing and there is nothing else to deliver.
		///
		/// A child of another machine is not resolved here at all - it becomes a stub, and whatever
		/// its machine says later is turned into the same note by PutInPlace, which is why the two
		/// share the wording.
		/// </remarks>
		/// <param name="parent">the container the child will be put into; null for the root</param>
		/// <param name="fallback">
		/// what stays in the child's place, and carries the note, if it cannot be resolved.
		/// Null means the child is simply left out and the note goes to the container.
		/// </param>
		VfsNodeDef? ResolveChild( VfsNodeDef child, bool forceUNC, List<Guid>? usedGuids,
				List<RemoteStub> pending, VfsNodeDef? parent, VfsNodeDef? fallback = null )
		{
			try
			{
				var resolved = ResolveLocally( child, forceUNC, true, usedGuids, pending, parent );

				if( resolved is RemoteStub stub )
					stub.Fallback = fallback;

				return resolved;
			}
			catch( Exception ex )
			{
				log.Warn( $"Could not resolve {child}: {ex.Message}" );

				// with nowhere to hang the note, the caller is the one who has to hear about it
				var target = fallback ?? parent;
				if( target is null )
					throw;

				( target.Notes ??= new List<string>() ).Add( CouldNotLookUp( child, ex ) );

				return null;
			}
		}

		/// <summary>Why something the operator asked for is not in the tree.</summary>
		static string CouldNotLookUp( VfsNodeDef node, Exception ex )
			=> $"{DescribeForNote( node )} could not be looked up, so nothing of it is here:"
			+ $" {Tools.JustFirstLine( ex.Message )}";

		/// <summary>
		/// A node as a person would name it - for a note read by somebody holding the archive and
		/// nothing else, so it says which machine and which application it belonged to.
		/// </summary>
		static string DescribeForNote( VfsNodeDef node )
		{
			var name = !string.IsNullOrEmpty( node.Title ) ? node.Title
					 : !string.IsNullOrEmpty( node.Id ) ? node.Id
					 : node.Path ?? node.Guid.ToString();

			var where = string.Empty;
			if( !string.IsNullOrEmpty( node.AppId ) ) where += $" of {node.AppId}";
			if( !string.IsNullOrEmpty( node.MachineId ) ) where += $" on {node.MachineId}";

			return $"'{name}'{where}";
		}

		VfsNodeDef? ResolveFolder( FolderDef folderDef, bool forceUNC, bool includeContent )
		{
			var rootNode = EmptyFrom<VFolderDef>( folderDef );
			rootNode.Path = ResolveFilePath( folderDef, forceUNC );

			if( string.IsNullOrEmpty( rootNode.Path ) )
				return null;

			if( !includeContent )
				return rootNode;

			FileScan.Result scan;
			try
			{
				scan = FileScan.FindMatchingFiles(
					rootNode.Path,
					folderDef.Mask,
					folderDef.MaxSeconds,
					folderDef.MaxFiles,
					folderDef.MaxTotalBytes,
					recursive: true,
					tailBytes: folderDef.TailBytes
				);
			}
			catch( Exception ex ) // folder not exists or not accessible?
			{
				// Not swallowed any more. As a member of a container this becomes a note there and
				// the rest of the package is collected as usual; asked for on its own it fails, which
				// is the honest answer to "give me this folder" when the folder is not there.
				log.Debug( $"ResolveFolder failed: {folderDef} Error: {ex.Message}" );
				throw;
			}

			// what the size budget pushed out is worth saying out loud - the user asked for those files
			if( scan.Skipped.Count > 0 )
			{
				var note = $"{scan.Skipped.Count} file(s) of '{rootNode.Path}' left out, over the "
						+ $"{folderDef.MaxTotalBytes} byte limit of node '{folderDef.Id}': "
						+ string.Join( ", ", scan.Skipped.Take( 20 ).Select( s => $"{s.RelPath} ({s.Bytes} B)" ) )
						+ ( scan.Skipped.Count > 20 ? ", ..." : "" );

				log.Warn( note );
				( rootNode.Notes ??= new List<string>() ).Add( note );
			}

			// build the tree of virtual subfolders mirroring the location of the files within the scanned folder
			foreach( var (relPath, info) in scan.Files )
			{
				var parent = GetOrCreateSubFolder( rootNode, System.IO.Path.GetDirectoryName( relPath ), folderDef );

				parent.Children.Add(
					new FileDef
					{
						Path = info.FullName,
						MachineId = folderDef.MachineId,
						AppId = folderDef.AppId,
						IsContainer = false,
						Title = info.Name,
						TailBytes = folderDef.TailBytes, // a folder's setting applies to its files
						Clearable = folderDef.Clearable, // and so does the permission
					}
				);
			}

			return rootNode;
		}

		/// <summary>
		/// Finds (or creates) the chain of virtual subfolders for given folder path relative to the root node.
		/// Returns the deepest one, or the root node itself if the relative path is empty.
		/// </summary>
		static VfsNodeDef GetOrCreateSubFolder( VfsNodeDef rootNode, string? relDirPath, FolderDef folderDef )
		{
			var current = rootNode;

			if( string.IsNullOrEmpty( relDirPath ) )
				return current;

			var pathSoFar = rootNode.Path ?? string.Empty;

			foreach( var segment in relDirPath.Split( new char[]{'/','\\'}, StringSplitOptions.RemoveEmptyEntries ) )
			{
				pathSoFar = System.IO.Path.Combine( pathSoFar, segment );

				var subFolder = current.Children.Find(
					x => x.IsContainer && string.Equals( x.Title, segment, StringComparison.OrdinalIgnoreCase )
				);

				if( subFolder is null )
				{
					subFolder = new VFolderDef
					{
						Path = pathSoFar,
						MachineId = folderDef.MachineId,
						AppId = folderDef.AppId,
						IsContainer = true,
						Title = segment,
					};
					current.Children.Add( subFolder );
				}

				current = subFolder;
			}

			return current;
		}

		List<string> GetNewestFilesInFolder( string folderName, string mask, int maxFiles, double maxAgeSeconds )
		{
			// the mask of the 'Newest' filter applies to the files in the given folder only, never to subfolders
			var scan = FileScan.FindMatchingFiles( folderName, mask, maxAgeSeconds, maxFiles, 0, recursive: false );

			return ( from x in scan.Files select x.Info.FullName ).ToList();
		}

	}
}

