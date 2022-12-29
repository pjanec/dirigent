using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Dirigent;

namespace Dirigent.Scripts.BuiltIn
{

public class ResolveVfsPath : Script
{
	private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

	public static readonly string _Name = "BuiltIns/ResolveVfsPath.cs";

	//[MessagePack.MessagePackObject]
	public class TArgs
	{
		//[MessagePack.Key( 1 )]
		public VfsNodeDef? VfsNode;

		//[MessagePack.Key( 2 )]
		public bool ForceUNC;

		//[MessagePack.Key( 3 )]
		public bool IncludeContent;

		public override string ToString() => $"{VfsNode}";
		public byte[] Serialize() => Tools.Serialize( this );
		public static TResult? Deserialize( byte[] data ) => Tools.Deserialize<TResult>( data );
	};

	//[MessagePack.MessagePackObject]
	public class TResult
	{
		//[MessagePack.Key( 1 )]
		public VfsNodeDef? VfsNode;

		public override string ToString() => $"{VfsNode}";
		public byte[] Serialize() => Tools.Serialize( this );
		public static TResult? Deserialize( byte[] data ) => Tools.Deserialize<TResult>( data );
	}

	protected async override Task<byte[]?> Run( CancellationToken ct )
	{
		var args = Tools.Deserialize<TArgs>( Args );
		if( args is null ) throw new NullReferenceException("Args == null");

		var vfsNode = args.VfsNode;
		if( vfsNode is null ) throw new NullReferenceException("vfsNode == null");


		var result = new TResult { VfsNode = await Dirig.ResolveAsync( vfsNode, args.ForceUNC, args.IncludeContent ) };
		return result.Serialize();
	}
}

}
