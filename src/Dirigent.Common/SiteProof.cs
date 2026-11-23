using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Dirigent.Net
{
	/// <summary>
	/// The proof that both ends of a connection to the master know the site secret, bound to the TLS session they are in.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The master's certificate is self-signed and made anew at every start, so nothing can be pinned and TLS alone proves nothing about who
	/// is at the other end. What does is the secret: each side shows with an HMAC that it holds it, over both nonces (so that no proof can be
	/// replayed) and over the hash of the master's certificate (so that a relay between them cannot pass a proof on: the client computes it over the
	/// certificate <i>it</i> was shown, the master over its own, and for a relay those are two different certificates).
	/// </para>
	/// <para>
	/// The two proofs start with different labels, so a master's proof is no use as a client's: whoever holds one cannot turn it into the other.
	/// All the parts of what is signed have a fixed length - a label, two 32-byte nonces and a 32-byte hash - so they cannot be shifted against each other.
	/// </para>
	/// </remarks>
	public static class SiteProof
	{
		public const int NonceLength = 32;
		public const int HashLength = 32;

		public const string MasterLabel = "dirigent-master";
		public const string ClientLabel = "dirigent-client";

		/// <summary>A fresh nonce from the system's random number generator.</summary>
		public static byte[] NewNonce() => RandomNumberGenerator.GetBytes( NonceLength );

		/// <summary>The SHA-256 of a certificate as it was sent over the wire (its DER).</summary>
		public static byte[] CertHash( X509Certificate certificate ) => SHA256.HashData( certificate.GetRawCertData() );

		/// <summary>HMAC-SHA256( secret, "dirigent-master" | clientNonce | masterNonce | certHash ): what the master says to the client.</summary>
		public static byte[] MasterProof( byte[] secret, byte[] clientNonce, byte[] masterNonce, byte[] certHash )
			=> Compute( MasterLabel, secret, clientNonce, masterNonce, certHash );

		/// <summary>HMAC-SHA256( secret, "dirigent-client" | clientNonce | masterNonce | certHash ): what the client says to the master.</summary>
		public static byte[] ClientProof( byte[] secret, byte[] clientNonce, byte[] masterNonce, byte[] certHash )
			=> Compute( ClientLabel, secret, clientNonce, masterNonce, certHash );

		/// <summary>Whether two proofs are the same, in a time that does not depend on how much of them agrees.</summary>
		public static bool Matches( byte[]? expected, byte[]? received )
			=> expected is not null && received is not null && CryptographicOperations.FixedTimeEquals( expected, received );

		static byte[] Compute( string label, byte[] secret, byte[] clientNonce, byte[] masterNonce, byte[] certHash )
		{
			if( secret.Length == 0 ) throw new ArgumentException( "no secret", nameof( secret ) );
			if( clientNonce.Length != NonceLength ) throw new ArgumentException( "a nonce is 32 bytes", nameof( clientNonce ) );
			if( masterNonce.Length != NonceLength ) throw new ArgumentException( "a nonce is 32 bytes", nameof( masterNonce ) );
			if( certHash.Length != HashLength ) throw new ArgumentException( "a certificate hash is 32 bytes", nameof( certHash ) );

			using var hmac = new HMACSHA256( secret );
			var labelBytes = Encoding.ASCII.GetBytes( label );
			var data = new byte[labelBytes.Length + NonceLength * 2 + HashLength];
			int o = 0;
			Buffer.BlockCopy( labelBytes, 0, data, o, labelBytes.Length ); o += labelBytes.Length;
			Buffer.BlockCopy( clientNonce, 0, data, o, NonceLength ); o += NonceLength;
			Buffer.BlockCopy( masterNonce, 0, data, o, NonceLength ); o += NonceLength;
			Buffer.BlockCopy( certHash, 0, data, o, HashLength );
			return hmac.ComputeHash( data );
		}
	}

	/// <summary>The certificate the master presents on a secured port.</summary>
	public static class MasterCertificate
	{
		/// <summary>
		/// A new self-signed ECDSA P-256 certificate, valid for ten years, made in memory: no file, nothing kept for the next start - nothing
		/// pins it, the proof of the site secret binds whatever certificate it is. Dispose it when the server is done with it.
		/// </summary>
		public static X509Certificate2 Create()
		{
			using var key = ECDsa.Create( ECCurve.NamedCurves.nistP256 );
			var request = new CertificateRequest( "CN=Dirigent master", key, HashAlgorithmName.SHA256 );
			request.CertificateExtensions.Add( new X509KeyUsageExtension( X509KeyUsageFlags.DigitalSignature, critical: true ) );
			request.CertificateExtensions.Add( new X509EnhancedKeyUsageExtension( new OidCollection { new Oid( "1.3.6.1.5.5.7.3.1" ) }, critical: false ) ); // server authentication

			using var ephemeral = request.CreateSelfSigned( DateTimeOffset.UtcNow.AddDays( -1 ), DateTimeOffset.UtcNow.AddYears( 10 ) );

			// SChannel (SslStream on Windows) cannot use the key of a certificate that was only ever in memory; going through a PFX gives it one it can.
			// The key lives with the returned certificate and goes when that is disposed.
			return new X509Certificate2( ephemeral.Export( X509ContentType.Pfx ), (string?) null, X509KeyStorageFlags.Exportable );
		}
	}
}
