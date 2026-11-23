using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dirigent
{
	/// <summary>What became of a token: who and what it allows, or why it is not accepted.</summary>
	public sealed class TokenResult
	{
		public bool Ok { get; init; }

		/// <summary>Why it is not accepted, for the caller and the log. Null when it is.</summary>
		public string? Error { get; init; }

		/// <summary>The <c>sub</c> claim: who the token is for, for the log.</summary>
		public string? Subject { get; init; }

		/// <summary>What the token allows, from <c>scope</c> and <c>scp</c>. Additive: <c>control</c> does not imply <c>read</c>.</summary>
		public IReadOnlySet<string> Scopes { get; init; } = new HashSet<string>();

		public DateTimeOffset? Expires { get; init; }

		public static TokenResult Fail( string error ) => new() { Ok = false, Error = error };
	}

	/// <summary>
	/// Checks a JWT of the web API by hand - no package: ES256 (ECDSA over P-256 and SHA-256) against the public key in <c>remoting-public.pem</c>.
	/// </summary>
	/// <remarks>
	/// Dirigent issues nothing and stores nothing: whoever holds the private key mints tokens, and this says whether one is genuine, current and
	/// meant for us. The header's <c>alg</c> must be exactly <c>ES256</c> - not <c>none</c>, not a keyed hash (a public key must never be taken for an HMAC
	/// secret) - and <c>exp</c> is required, so a token cannot be made that never ends. The clocks of the issuer and of the master may differ by
	/// <see cref="ClockTolerance"/>.
	/// </remarks>
	public sealed class TokenValidator
	{
		/// <summary>How far the master's clock and the issuer's may differ.</summary>
		public static readonly TimeSpan ClockTolerance = TimeSpan.FromSeconds( 60 );

		/// <summary>A token longer than this is not looked at.</summary>
		public const int MaxTokenLength = 16 * 1024;

		readonly ECDsa _key;
		readonly string? _audience;
		readonly Func<DateTimeOffset> _now;

		/// <param name="publicKeyPem">the public key, PEM ("BEGIN PUBLIC KEY")</param>
		/// <param name="audience">if set, the token's <c>aud</c> (a string or an array) must contain it</param>
		/// <exception cref="SecurityConfigException">the text is not an EC public key on P-256</exception>
		public TokenValidator( string publicKeyPem, string? audience = null, Func<DateTimeOffset>? now = null )
		{
			_audience = string.IsNullOrWhiteSpace( audience ) ? null : audience.Trim();
			_now = now ?? ( () => DateTimeOffset.UtcNow );

			try
			{
				_key = ECDsa.Create();
				_key.ImportFromPem( publicKeyPem );
			}
			catch( Exception ex )
			{
				throw new SecurityConfigException( $"The token public key is not a PEM public key of an elliptic curve: {ex.Message}" );
			}

			if( _key.KeySize != 256 )
				throw new SecurityConfigException( $"The token public key is a {_key.KeySize}-bit curve; ES256 needs P-256." );
		}

		/// <summary>Checks the token (the part after "Bearer ").</summary>
		public TokenResult Validate( string? token )
		{
			if( string.IsNullOrWhiteSpace( token ) ) return TokenResult.Fail( "no token" );
			token = token.Trim();
			if( token.Length > MaxTokenLength ) return TokenResult.Fail( "malformed token" );

			var parts = token.Split( '.' );
			if( parts.Length != 3 ) return TokenResult.Fail( "malformed token" );

			byte[] headerBytes, payloadBytes, signature;
			try
			{
				headerBytes = Base64Url( parts[0] );
				payloadBytes = Base64Url( parts[1] );
				signature = Base64Url( parts[2] );
			}
			catch( FormatException )
			{
				return TokenResult.Fail( "malformed token" );
			}

			JsonElement header, payload;
			try
			{
				using var h = JsonDocument.Parse( headerBytes );
				using var p = JsonDocument.Parse( payloadBytes );
				header = h.RootElement.Clone();
				payload = p.RootElement.Clone();
			}
			catch( JsonException )
			{
				return TokenResult.Fail( "malformed token" );
			}

			if( header.ValueKind != JsonValueKind.Object || payload.ValueKind != JsonValueKind.Object )
				return TokenResult.Fail( "malformed token" );

			// the algorithm is ours to say, not the token's: exactly ES256 or nothing
			if( !header.TryGetProperty( "alg", out var alg ) || alg.ValueKind != JsonValueKind.String || alg.GetString() != "ES256" )
				return TokenResult.Fail( "unsupported algorithm (only ES256 is accepted)" );

			if( signature.Length != 64 )
				return TokenResult.Fail( "bad signature" );

			bool genuine;
			try
			{
				genuine = _key.VerifyData( Encoding.ASCII.GetBytes( parts[0] + "." + parts[1] ), signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation );
			}
			catch( CryptographicException )
			{
				genuine = false;
			}
			if( !genuine ) return TokenResult.Fail( "bad signature" );

			var now = _now();

			if( !payload.TryGetProperty( "exp", out var expEl ) || !TryNumericDate( expEl, out var exp ) )
				return TokenResult.Fail( "no expiry (exp is required)" );
			if( now > exp + ClockTolerance )
				return TokenResult.Fail( $"expired at {exp.UtcDateTime:yyyy-MM-dd HH:mm:ss}Z" );

			if( payload.TryGetProperty( "nbf", out var nbfEl ) )
			{
				if( !TryNumericDate( nbfEl, out var nbf ) ) return TokenResult.Fail( "malformed token" );
				if( now < nbf - ClockTolerance )
					return TokenResult.Fail( $"not valid yet (valid from {nbf.UtcDateTime:yyyy-MM-dd HH:mm:ss}Z)" );
			}

			if( _audience is not null )
			{
				var audiences = Strings( payload, "aud" );
				if( !audiences.Contains( _audience ) )
					return TokenResult.Fail( $"wrong audience (this Dirigent is '{_audience}')" );
			}

			var scopes = new HashSet<string>( StringComparer.Ordinal );
			foreach( var s in Strings( payload, "scope" ) ) foreach( var one in s.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) ) scopes.Add( one );
			foreach( var s in Strings( payload, "scp" ) ) foreach( var one in s.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) ) scopes.Add( one );

			string? sub = payload.TryGetProperty( "sub", out var subEl ) && subEl.ValueKind == JsonValueKind.String ? subEl.GetString() : null;

			return new TokenResult { Ok = true, Subject = sub, Scopes = scopes, Expires = exp };
		}

		/// <summary>A claim that is a string or an array of strings, as a list.</summary>
		static List<string> Strings( JsonElement payload, string name )
		{
			var list = new List<string>();
			if( !payload.TryGetProperty( name, out var el ) ) return list;

			if( el.ValueKind == JsonValueKind.String ) list.Add( el.GetString() ?? "" );
			else if( el.ValueKind == JsonValueKind.Array )
				foreach( var item in el.EnumerateArray() )
					if( item.ValueKind == JsonValueKind.String ) list.Add( item.GetString() ?? "" );
			return list;
		}

		static bool TryNumericDate( JsonElement el, out DateTimeOffset value )
		{
			value = default;
			if( el.ValueKind != JsonValueKind.Number || !el.TryGetDouble( out var seconds ) ) return false;
			if( double.IsNaN( seconds ) || Math.Abs( seconds ) > 253402300799d ) return false; // year 9999
			value = DateTimeOffset.FromUnixTimeMilliseconds( (long) ( seconds * 1000 ) );
			return true;
		}

		/// <summary>base64url without padding, as JWT writes it.</summary>
		public static byte[] Base64Url( string text )
		{
			var s = text.Replace( '-', '+' ).Replace( '_', '/' );
			switch( s.Length % 4 )
			{
				case 0: break;
				case 2: s += "=="; break;
				case 3: s += "="; break;
				default: throw new FormatException( "not base64url" );
			}
			return Convert.FromBase64String( s );
		}
	}
}
