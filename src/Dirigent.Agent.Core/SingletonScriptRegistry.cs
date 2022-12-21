
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System;
using System.Text.RegularExpressions;

namespace Dirigent
{

	/// <summary>
	/// Maintains the definitions for the single-instance scripts loaded from shared config.
	/// Starts/kills the scripts on demand.
	/// </summary>
	public class SingletonScriptRegistry : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public class Entry : Disposable
		{
			public Guid Guid => Def.Guid;

			public ScriptDef Def { get; private set; }
			LocalScriptRegistry _localScriptRegistry;

			public Entry( ScriptDef def, ScriptFactory factory, LocalScriptRegistry localScriptRegistry )
			{
				Def = def;
				_localScriptRegistry = localScriptRegistry;
			}
			
			protected override void Dispose( bool disposing )
			{
				base.Dispose( disposing );
				if( !disposing ) return;
				_localScriptRegistry.Dispose();
			}

			public void Tick() {}

			public void Start( string? requestorId, string? args )
			{
				if (args is null) args = Def.Args;
				// create a new instance of the script; it will be disposed when it dies
				_localScriptRegistry.Start( Def.Guid, Def.Name, null, Tools.Serialize( args ), Def.Title, requestorId );
			}

			public void Stop()
			{
				_localScriptRegistry.Stop( Def.Guid );
			}
		}
		
		
		private Dictionary<Guid, Entry> _scripts = new Dictionary<Guid, Entry>();
		public Dictionary<Guid, Entry> Scripts => _scripts;

		Master _master;
		LocalScriptRegistry _localScriptRegistry;

		public SingletonScriptRegistry( Master master, LocalScriptRegistry localScriptRegistry )
		{
			_master = master;
			_localScriptRegistry = localScriptRegistry;
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;
			foreach (var x in _scripts.Values ) x.Dispose();
			_scripts.Clear();
		}

		public void SetAll( IEnumerable<ScriptDef> allDefs )
		{
			foreach (var x in _scripts.Values ) x.Dispose();
			_scripts.Clear();

			foreach( var def in allDefs )
			{
				var script = new Entry( def, _master.ScriptFactory, _localScriptRegistry );

				_scripts[script.Guid] = script;
			}

		}

		public Entry FindScript( Guid id )
		{
			if( Scripts.TryGetValue( id, out var scr ) )
			{
				return scr;
			}
			else
			{
				throw new Exception($"Unknown script {id}");
			}
		}
							
		public bool Contains( Guid id )
		{
			return Scripts.ContainsKey( id );
		}

		public void Tick()
		{
			foreach( var p in _scripts.Values )
			{
				p.Tick();
			}
		}

		// if args==null, use arguments from ScriptDef
		public void StartScript( string? requestorId, Guid id, string? args )
		{
			var entry = FindScript( id );
			entry.Start( requestorId, args );
		}

		public void KillScript( string requestorId, Guid id )
		{
			var entry = FindScript( id );
			entry.Stop();
		}

	}
}

