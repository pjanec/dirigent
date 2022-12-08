using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent.Scripts.ResolveVfsTree
{
	//public class Worker : TaskWorkerScript
	//{
	//	private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

	//	public static readonly string _Name = "BuiltIns/ResolveVfsTree.Worker";

	//	[ProtoBuf.ProtoContract]
	//	public class TArgs
	//	{
	//		[ProtoBuf.ProtoMember( 1 )]
	//		public List<VfsNodeDef> VfsNodes = new List<VfsNodeDef>();
	//	};

	//	protected override Task<byte[]?> Run( CancellationToken ct )
	//	{
	//		log.Info($"{Title} Run!");

	//		if( Args is null )
	//		{
	//			throw new Exception( "Args is null" );
	//		}
			
	//		// get args
			
	//		var vfsNodes = Tools.ProtoDeserialize<TArgs>( Args ).VfsNodes;

	//		// resolve paths
	//		// ...
			
	//		// return resolved paths
	//		var result = new TArgs { VfsNodes = vfsNodes }; // yes, same struct as the args
			
	//		//return Tools.ProtoSerialize( result ); // this would work if this method was marked async
	//		return Task.FromResult<byte[]?>( Tools.ProtoSerialize( result ) ); // this one needed for non-async method signature
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
