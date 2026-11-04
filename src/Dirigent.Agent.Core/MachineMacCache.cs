using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dirigent;

/// <summary>
/// What the master has learned about the network adapters of each machine, kept across restarts.
/// </summary>
/// <remarks>
/// Wake-on-LAN needs the MAC of a machine that is off, and the only moment a machine can tell it is
/// while it is on and connected. So agents report their adapters on every connect and the master
/// remembers the last report of each machine on disk: the master may well be restarted - or the
/// machine never switched on again - between the report and the wake-up.
///
/// A report replaces what was known of that machine wholesale: a swapped computer under the same id
/// must not leave the old computer's MAC behind to be woken in vain.
/// </remarks>
public class MachineMacCache
{
	private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

	public const string FileName = "MachineMacs.json";

	readonly object _lock = new();
	readonly string _filePath;
	Dictionary<string, List<NicInfo>> _nics = new();

	/// <summary>How many times the file was written; lets a test tell a skipped write from a repeated one.</summary>
	public int WriteCount { get; private set; }

	public string FilePath => _filePath;

	/// <param name="folder">
	/// Where to keep the file. Empty or null selects the default %LocalAppData%\Dirigent, the same as
	/// <see cref="AgentStateSaverLoader"/> - machine-global, hence the option, so that a test run or a
	/// second installation can be kept apart.
	/// </param>
	public MachineMacCache( string? folder )
	{
		if( string.IsNullOrEmpty( folder ) )
		{
			var localAppDataPath = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );
			folder = Path.Combine( localAppDataPath, "Dirigent" );
		}
		_filePath = Path.Combine( folder, FileName );

		Load();
	}

	/// <summary>
	/// Remembers the adapters a machine reported.
	/// </summary>
	/// <remarks>
	/// An empty report is ignored: it comes from a client too old to say or one that failed to read
	/// them, and must not erase what an earlier, better report established.
	/// The file is written only when something changed - agents reconnect all the time.
	/// </remarks>
	public void Update( string machineId, IEnumerable<NicInfo> nics )
	{
		var list = nics.ToList();
		if( list.Count == 0 || string.IsNullOrEmpty( machineId ) ) return;

		lock( _lock )
		{
			if( _nics.TryGetValue( machineId, out var known ) && known.SequenceEqual( list ) ) return;

			_nics[machineId] = list;
			Save();
		}
	}

	/// <summary>The adapters last reported by the machine; empty if never heard of.</summary>
	public List<NicInfo> Get( string machineId )
	{
		lock( _lock )
		{
			return _nics.TryGetValue( machineId, out var list ) ? new List<NicInfo>( list ) : new List<NicInfo>();
		}
	}

	void Load()
	{
		try
		{
			if( !File.Exists( _filePath ) ) return;

			var loaded = JsonConvert.DeserializeObject<Dictionary<string, List<NicInfo>>>( File.ReadAllText( _filePath ) );
			if( loaded is not null ) _nics = loaded;
		}
		catch( Exception e )
		{
			// the same as never having heard of any machine; the agents will say again when they connect
			log.Warn( $"Could not read the machine MAC addresses from '{_filePath}': {e.Message}" );
			_nics = new();
		}
	}

	/// <summary>
	/// Writes through a temporary name, so that a file found on disk is always a whole one.
	/// </summary>
	void Save()
	{
		try
		{
			var folder = Path.GetDirectoryName( _filePath );
			if( !string.IsNullOrEmpty( folder ) ) Directory.CreateDirectory( folder );

			var temp = _filePath + ".writing";
			File.WriteAllText( temp, JsonConvert.SerializeObject( _nics, Formatting.Indented ) );
			File.Move( temp, _filePath, true );
			WriteCount++;
		}
		catch( Exception e )
		{
			log.Error( $"Could not write the machine MAC addresses to '{_filePath}': {e.Message}" );
		}
	}
}
