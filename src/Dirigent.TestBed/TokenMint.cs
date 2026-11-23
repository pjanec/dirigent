using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dirigent.TestBed
{
	/// <summary>
	/// Makes the tokens of the web API the way whoever issues them would: an ES256 key pair, the public half as PEM for the master's
	/// <c>remoting-public.pem</c>, and JWTs signed with the private half. For tests only - Dirigent issues nothing.
	/// </summary>
	public sealed class TokenMint : IDisposable
	{
		public ECDsa Key { get; } = ECDsa.Create( ECCurve.NamedCurves.nistP256 );

		/// <summary>What goes into remoting-public.pem.</summary>
		public string PublicKeyPem => Key.ExportSubjectPublicKeyInfoPem();

		public static string B64( byte[] data ) => Convert.ToBase64String( data ).TrimEnd( '=' ).Replace( '+', '-' ).Replace( '/', '_' );

		/// <summary>A token for <paramref name="scope"/> valid for <paramref name="lifetime"/> from <paramref name="now"/>.</summary>
		public string Mint( string scope, string subject = "tester", TimeSpan? lifetime = null, DateTimeOffset? now = null, object? audience = null, ECDsa? signWith = null )
		{
			var issued = now ?? DateTimeOffset.UtcNow;
			var payload = new Dictionary<string, object>
			{
				["sub"] = subject,
				["scope"] = scope,
				["exp"] = issued.Add( lifetime ?? TimeSpan.FromHours( 1 ) ).ToUnixTimeSeconds(),
			};
			if( audience is not null ) payload["aud"] = audience;
			return Mint( payload, signWith: signWith );
		}

		/// <summary>A token with exactly these claims, and a header that says <paramref name="alg"/>. Signed ES256 unless <paramref name="alg"/> asks for none or HS256.</summary>
		public string Mint( IDictionary<string, object> payload, string alg = "ES256", ECDsa? signWith = null )
		{
			var header = B64( Encoding.UTF8.GetBytes( JsonSerializer.Serialize( new Dictionary<string, object> { ["alg"] = alg, ["typ"] = "JWT" } ) ) );
			var body = B64( Encoding.UTF8.GetBytes( JsonSerializer.Serialize( payload ) ) );
			var data = header + "." + body;

			byte[] signature;
			switch( alg )
			{
				case "none": signature = Array.Empty<byte>(); break;
				case "HS256": // the old trick: the public key used as an HMAC secret
					using( var h = new HMACSHA256( Encoding.UTF8.GetBytes( PublicKeyPem ) ) ) signature = h.ComputeHash( Encoding.ASCII.GetBytes( data ) );
					break;
				default:
					signature = ( signWith ?? Key ).SignData( Encoding.ASCII.GetBytes( data ), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation );
					break;
			}
			return data + "." + B64( signature );
		}

		public void Dispose() => Key.Dispose();
	}
}
