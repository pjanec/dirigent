
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

	public delegate string? GetMachineIPDelegate( string machineId );

	/// <summary>
	/// List of registered files and packages
	/// </summary>
	public class MachineRegistry
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public class TMachine
		{
			GetMachineIPDelegate _machineIPDelegate;
			
			public string Id = string.Empty;
			public MachineDef Def;

			public TMachine( MachineDef def, GetMachineIPDelegate machineIPDelegate )
			{
				Def = def;
				_machineIPDelegate = machineIPDelegate;
			}

			public string IP
			{
				get
				{
					string? ip = Def.IP;

					if( !string.IsNullOrEmpty( ip ) )
					{
						return ip;
					}

					// find machine IP
					if( string.IsNullOrEmpty( ip ) )
					{
						if( _machineIPDelegate != null && !string.IsNullOrEmpty( Def.Id ) )
						{
							ip = _machineIPDelegate( Def.Id );
						}
					}

					if( string.IsNullOrEmpty( ip ) )
						throw new Exception($"Could not find IP of machine {Def.Id}");

					// remember
					Def.IP = ip;

					return ip;
				}
			}

		}

		public Dictionary<string, TMachine> _machines { get; private set; } = new();
		public Dictionary<string, TMachine> Machines => _machines;
		
		string _localMachineId;

		public string LocalMachineId => _localMachineId;

		public GetMachineIPDelegate _machineIPDelegate;

		public MachineRegistry( string localMachineId, GetMachineIPDelegate machineIPDelegate )
		{
			_localMachineId = localMachineId;
			_machineIPDelegate = machineIPDelegate;
		}
		
		public MachineDef? GetMachineDef( string Id )
		{
			if( _machines.TryGetValue( Id, out var m ) )
			{
				return m.Def;
			}
			return null;
		}

		public IEnumerable<MachineDef> GetAllMachineDefs()
		{
			return from x in _machines select x.Value.Def;
		}

		public void Clear()
		{
			_machines.Clear();
		}

		public void SetMachines( IEnumerable<MachineDef> machines )
		{
			_machines.Clear();
			foreach( var mdef in machines )
			{
				var m = new TMachine( mdef, _machineIPDelegate );
				_machines[mdef.Id] = m;
			}
		}

		public bool IsOnline( string? clientId )
		{
			if (clientId is null)
				return false;
			
			var clientIP = _machineIPDelegate( clientId );
			if( clientIP is null )
				return false;

			return true;
		}

		public bool IsLocal( string? clientId )
		{
			if (string.IsNullOrEmpty( clientId ))
				return false;
				
			if ( clientId == _localMachineId )
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

		public bool IsMachineConnected( string machineId )
		{
			// TODO
			return false;
		}

		// Returns the predefined IP if defined, or actual IP of the machine if connected, otherwise throws.
		// If the machine listed in preconfigured machines, add a fake machine definition having just the IP.
		public string GetMachineIP( string? machineId )
		{
			string? ip = null;

			if( machineId is null )
				throw new Exception($"Null machine id");

			// find machine
			if( Machines.TryGetValue( machineId, out var m ) )
			{
				return m.IP;
			}
				
			if( !string.IsNullOrEmpty( machineId ) )
			{
				ip = _machineIPDelegate( machineId );
			}

			if( string.IsNullOrEmpty( ip ) )
				throw new Exception($"Could not find IP of machine {machineId}");

			// add a record having just the IP (as nothing else is known about the machine)
			var mdef = new MachineDef()
			{
				Id = machineId,
				IP = ip,
			};
			m = new TMachine( mdef, _machineIPDelegate );
			Machines[machineId] = m;

			return ip;

		}

	}
}

