using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Dirigent;

public class DemoScript1 : Script
{
	private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

	[ProtoBuf.ProtoContract]
	public class Result
	{
		[ProtoBuf.ProtoMember( 1 )]
		public int Code;

		public override string ToString() => Code.ToString();
		public byte[] Serialize() => Tools.ProtoSerialize( this );
		public static Result? Deserialize( byte[] data ) => Tools.ProtoDeserialize<Result>( data );
	}

	protected async override Task<byte[]?> Run( CancellationToken ct )
	{
		if( Args is not null )
			log.Info($"Init with args: '{Encoding.UTF8.GetString(Args)}'");

		log.Info("Run!");

		SetStatus("Waiting for m1 to boot");

		//// wait for agent m1 to boot
		//while( await Dirig.GetClientStateAsync("m1") is null ) await Task.Delay(100, ct);

		// start app "m1.a" defined within "plan1"
		await StartApp( "m1.a", "plan1" );
		
		// wait for the app to initialize
		SetStatus("Waiting for m1.a to initialize");
		while ( !(await GetAppState( "m1.a" ))!.Initialized ) await Task.Delay(100, ct);

		// start app "m1.b" defined within "plan1"
		await StartApp( "m1.b", "plan1" );


		//SetStatus("Waiting before throwing exception");
		//await Task.Delay(4000, ct);
		//throw new Exception( "Demo exception" );

		SetStatus("Waiting before terminating");
		await Task.Delay(4000, ct);

		await KillApp( "m1.a" );
		await KillApp( "m1.b" );

		return new Result { Code = 17 }.Serialize();
	}
}
