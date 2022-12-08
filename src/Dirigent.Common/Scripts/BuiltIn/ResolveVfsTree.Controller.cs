using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent.Scripts.ResolveVfsTree
{
	//public class Controller : TaskControllerScript
	//{
	//	private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

	//	public static readonly string _Name = "BuiltIns/ResolveVfsTree.Controller";

	//	class MachineRec
	//	{
	//		public List<VfsNodeDef> VfsNodes = new List<VfsNodeDef>();
	//		public Guid WorkerInstance;
	//	}

	//	Dictionary<string, MachineRec> _machineRecs = new Dictionary<string, MachineRec>();

	//	[ProtoBuf.ProtoContract]
	//	public class TArgs
	//	{
	//		[ProtoBuf.ProtoMember( 1 )]
	//		public List<VfsNodeDef> VfsNodes = new List<VfsNodeDef>();
	//	};

	//	protected async override Task<byte[]?> Run( CancellationToken ct )
	//	{
	//		log.Info($"{Title} Run!");

	//		if( Args is null )
	//		{
	//			throw new Exception( "Args is null" );
	//		}
			
	//		// get args
			
	//		var vfsNodes = Tools.ProtoDeserialize<TArgs>( Args ).VfsNodes;

	//		//
	//		// process per machine
	//		//

	//		// get all machines
	//		var machineIds = new List<string>();
	//		foreach( var node in vfsNodes )
	//		{
	//			ExtractMachineIds( node, machineIds );
	//		}

	//		// find out what clients are connected (to avoid sending to those not existing)
	//		var tmpClients = await Dirig.GetAllClientStates();
	//		if (tmpClients is null)
	//			throw new Exception( "existingClients is null" );
			
	//		var existingClients = (from i in tmpClients select i.Key).ToList();

	//		foreach ( var machineId in machineIds )
	//		{
	//			// skip non-connected machines
	//			if( !existingClients.Contains( machineId ) )
	//				continue;

	//			// remember machine record so that we can check for completion
	//			var rec = new MachineRec
	//			{
	//				VfsNodes = FilterByMachine( machineId, vfsNodes ),
	//				WorkerInstance = Guid.NewGuid(),
	//			};

	//			_machineRecs[machineId] = rec;

	//			// start worker on the machine
	//			await Dirig.Send( new Net.StartTaskWorkerMessage()
	//			{
	//				TaskInstance = TaskInstance,
	//				Workers = new List<string>() { machineId },
	//				WorkerInstances = new List<Guid> { rec.WorkerInstance },
	//				ScriptName = Scripts.ResolveVfsTree.Worker._Name,
	//				Args = Tools.ProtoSerialize( rec.VfsNodes ),
	//			} );

	//		}

	//		// wait for all workers to finish
	//		// HOW???
	//		//   ask IDirig for the status of script with given guid
	//		// 	 ReflStates catches the status of any script (something like TaskRegistryClient but for scripts - ScriptRegistryClient or something)
	//		//      status updates are sent for any script, the same way for permanent scripts as well as for distributed tasks (both controller and workers)
			
	//		//      ScriptRegistry catches ScriptState messages
	//		while (_machineRecs.Count > 0)
	//		{
	//			await Task.Delay( 1000, ct );
	//		}

			



	//		await WaitUntilCancelled(ct);

	//		return null;
	//	}

	//	void ExtractMachineIds( VfsNodeDef node, List<string> machineIds )
	//	{
	//		if( !string.IsNullOrEmpty( node.MachineId ) )
	//		{
	//			if (!machineIds.Contains( node.MachineId ))
	//			{
	//				machineIds.Add( node.MachineId );
	//			}
	//		}

	//		foreach (var child in node.Children)
	//		{
	//			ExtractMachineIds( child, machineIds );
	//		}
	//	}

	//	List<VfsNodeDef> FilterByMachine( string machineId, List<VfsNodeDef> vfsNodes )
	//	{
	//		return new List<VfsNodeDef>();
	//	}
	//}

}
