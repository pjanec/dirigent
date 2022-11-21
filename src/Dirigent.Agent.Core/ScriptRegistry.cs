
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
	/// List of currently known scripts including their current status
	/// SharedConfig-predefined scripts
	/// </summary>
	public class ScriptRegistry
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		// all currently running scripts
		private Dictionary<string, ScriptEntry> _scripts = new Dictionary<string, ScriptEntry>();
		public Dictionary<string, ScriptEntry> Scripts => _scripts;

		public Dictionary<string, ScriptState> ScriptStates => Scripts.Values.ToDictionary( p => p.Id, p => p.State );


		Master _master;
		public ScriptRegistry( Master master )
		{
			_master = master;
		}
		
		public void SetAll( IEnumerable<ScriptDef> allDefs )
		{
			_scripts.Clear();

			foreach( var def in allDefs )
			{
				var script = new ScriptEntry( def, _master );

				_scripts[script.Id] = script;
			}

		}

		/// <summary>
		/// Finds plan ba name. Throws if failed.
		/// </summary>
		public ScriptEntry FindScript( string id )
		{
			if( Scripts.TryGetValue( id, out var scr ) )
			{
				return scr;
			}
			else
			{
				throw new UnknownScriptId( id );
			}
		}

		public void Tick()
		{
			foreach( var p in _scripts.Values )
			{
				p.Tick();
			}
		}

		// if args==null, use arguments from ScriptDef
		public void StartScript( string requestorId, string id, string? args )
		{
			var predScr = FindScript( id );
			predScr.Start( args );
		}

		public void KillScript( string requestorId, string id )
		{
			var predScr = FindScript( id );
			predScr.Kill();
		}

		public ScriptState? GetScriptState( string id )
		{
			var predScr = FindScript( id );
			return predScr.State;
		}

	}
}

