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

		/// <summary>
		/// The node to resolve, named by its config id. Used when VfsNode is not given, which is the
		/// case for every caller but a GUI.
		/// </summary>
		//[MessagePack.Key( 4 )]
		public VfsNodeSelector? Node;

		//[MessagePack.Key( 2 )]
		public bool ForceUNC;

		//[MessagePack.Key( 3 )]
		public bool IncludeContent;

		public override string ToString() => $"{VfsNode}";
		public string Serialize() => Tools.Serialize( this );
		public static TResult? Deserialize( string data ) => Tools.Deserialize<TResult>( data );
	};

	//[MessagePack.MessagePackObject]
	public class TResult
	{
		//[MessagePack.Key( 1 )]
		public VfsNodeDef? VfsNode;

		public override string ToString() => $"{VfsNode}";
		public string Serialize() => Tools.Serialize( this );
		public static TResult? Deserialize( string data ) => Tools.Deserialize<TResult>( data );
	}

	protected async override Task<string?> Run()
	{
		var args = Tools.Deserialize<TArgs>( Args );
		if( args is null ) throw new NullReferenceException("Args == null");

		// a resolved tree if the caller had one, otherwise a reference for us to look up
		VfsNodeDef? vfsNode = args.VfsNode ?? args.Node?.ToFileRef();
		if( vfsNode is null ) throw new ArgumentException( "Neither VfsNode nor Node given." );


		var result = new TResult { VfsNode = await Dirig.ResolveAsync( vfsNode, args.ForceUNC, args.IncludeContent ) };
		return result.Serialize();
	}
}

}
