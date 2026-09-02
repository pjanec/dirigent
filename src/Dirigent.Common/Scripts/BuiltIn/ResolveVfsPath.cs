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

		/// <summary>
		/// Several nodes to resolve in a single call, answered by <see cref="TResult.Nodes"/> - one
		/// entry per node, in the order asked.
		/// </summary>
		/// <remarks>
		/// The whole point is the round trip: a machine takes about as long to resolve twenty of its
		/// nodes as one, and on a site of forty machines the operator waits for the trips, not for
		/// the work. Given, VfsNode and Node are ignored.
		/// </remarks>
		public List<VfsNodeDef>? VfsNodes;

		//[MessagePack.Key( 2 )]
		public bool ForceUNC;

		//[MessagePack.Key( 3 )]
		public bool IncludeContent;

		public override string ToString() => VfsNodes is not null ? $"{VfsNodes.Count} node(s)" : $"{VfsNode}";
		public string Serialize() => Tools.Serialize( this );
		public static TResult? Deserialize( string data ) => Tools.Deserialize<TResult>( data );
	};

	/// <summary>
	/// What became of one of the nodes of a batch.
	/// </summary>
	/// <remarks>
	/// A node of the batch that cannot be resolved must not cost the caller the other nineteen -
	/// each one is a separate thing the operator asked for, and they are only travelling together to
	/// save a round trip. So a failure is reported in its own slot rather than thrown.
	/// </remarks>
	public class TNodeResult
	{
		public VfsNodeDef? VfsNode;

		/// <summary>Why this node could not be resolved. Null when it was.</summary>
		public string? Error;
	}

	//[MessagePack.MessagePackObject]
	public class TResult
	{
		//[MessagePack.Key( 1 )]
		public VfsNodeDef? VfsNode;

		/// <summary>One entry per node of <see cref="TArgs.VfsNodes"/>, in the order asked.</summary>
		public List<TNodeResult>? Nodes;

		public override string ToString() => Nodes is not null ? $"{Nodes.Count} node(s)" : $"{VfsNode}";
		public string Serialize() => Tools.Serialize( this );
		public static TResult? Deserialize( string data ) => Tools.Deserialize<TResult>( data );
	}

	protected async override Task<string?> Run()
	{
		var args = Tools.Deserialize<TArgs>( Args );
		if( args is null ) throw new NullReferenceException("Args == null");

		if( args.VfsNodes is not null )
			return ( await ResolveMany( args ) ).Serialize();

		// a resolved tree if the caller had one, otherwise a reference for us to look up
		VfsNodeDef? vfsNode = args.VfsNode ?? args.Node?.ToFileRef();
		if( vfsNode is null ) throw new ArgumentException( "Neither VfsNode nor Node given." );


		var result = new TResult { VfsNode = await Dirig.ResolveAsync( vfsNode, args.ForceUNC, args.IncludeContent ) };
		return result.Serialize();
	}

	async Task<TResult> ResolveMany( TArgs args )
	{
		var result = new TResult { Nodes = new List<TNodeResult>() };

		foreach( var node in args.VfsNodes! )
		{
			try
			{
				result.Nodes.Add( new TNodeResult
				{
					VfsNode = await Dirig.ResolveAsync( node, args.ForceUNC, args.IncludeContent )
				} );
			}
			catch( Exception ex )
			{
				log.Warn( $"Could not resolve {node}: {ex.Message}" );
				result.Nodes.Add( new TNodeResult { Error = Tools.JustFirstLine( ex.Message ) } );
			}
		}

		return result;
	}
}

}
