using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dirigent
{
	/// <summary>Whether the master port is secured; see docs/Security.md. Never read from the SharedConfig.</summary>
	public enum ESecurityMode
	{
		/// <summary>Plain TCP as in every version before 3.1.20.</summary>
		Off,

		/// <summary>The master port is secured as with On; for the REST API every call is checked and logged but none refused.</summary>
		Audit,

		/// <summary>The master port is secured (TLS and the site secret), and so is REST.</summary>
		On,
	}

	/// <summary>A security setting that is wrong or a file that is missing: the process must not start (and never falls back to open).</summary>
	public class SecurityConfigException : Exception
	{
		public SecurityConfigException( string message ) : base( message ) {}
	}

	/// <summary>
	/// What a process knows about security: the mode, and - when the master port is secured - the site secret, read once at
	/// start. One class loads it, so that every place that needs the secret gets it from here and nowhere else reads a file.
	/// </summary>
	public sealed class SecuritySettings
	{
		public const string SiteSecretFileName = "site.secret";
		public const string TokenPublicKeyFileName = "remoting-public.pem";

		/// <summary>The shortest site secret accepted, in characters of the trimmed file content.</summary>
		public const int MinSecretLength = 32;

		public ESecurityMode Mode { get; }

		/// <summary>The master port is secured: TLS, and the proof of the site secret both ways.</summary>
		public bool Secured => Mode != ESecurityMode.Off;

		/// <summary>The key: the trimmed content of the secret file in UTF-8. Empty when the mode is Off.</summary>
		public byte[] SiteSecret { get; }

		/// <summary>Where the secret came from, for the log - a path, or "(in memory)". Never the secret itself.</summary>
		public string SiteSecretSource { get; }

		/// <summary>Where the public key that checks REST tokens was found; null if it was not looked for or does not exist (a step of its own needs it).</summary>
		public string? TokenPublicKeyPath { get; }

		/// <summary>Checks the tokens of the web API; set on a master that runs a web server while security is on or audit, otherwise null.</summary>
		public TokenValidator? Tokens { get; private init; }

		/// <summary>The audience a token must name, if one is required.</summary>
		public string? TokenAudience => _tokenAudience;
		string? _tokenAudience;

		/// <summary>The same settings with the web API's tokens checked against this public key (PEM) - for tests, without a file.</summary>
		public SecuritySettings WithTokens( string publicKeyPem, string? audience = null )
			=> new SecuritySettings( Mode, SiteSecret, SiteSecretSource, TokenPublicKeyPath ) { HandshakeTimeout = HandshakeTimeout, Tokens = new TokenValidator( publicKeyPem, audience ), _tokenAudience = audience };

		/// <summary>How long a connection to the master may take to be proved, TLS included, before it is closed.</summary>
		public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds( 10 );

		/// <summary>The same settings with another time allowed for the handshake - for tests that wait for it to run out.</summary>
		public SecuritySettings WithHandshakeTimeout( TimeSpan timeout )
			=> new SecuritySettings( Mode, SiteSecret, SiteSecretSource, TokenPublicKeyPath ) { HandshakeTimeout = timeout, Tokens = Tokens, _tokenAudience = _tokenAudience };

		SecuritySettings( ESecurityMode mode, byte[] secret, string source, string? tokenKeyPath )
		{
			Mode = mode;
			SiteSecret = secret;
			SiteSecretSource = source;
			TokenPublicKeyPath = tokenKeyPath;
		}

		/// <summary>Security off: nothing is read and nothing is secured.</summary>
		public static SecuritySettings Off { get; } = new( ESecurityMode.Off, Array.Empty<byte>(), "(security off)", null );

		/// <summary>A secret given in memory, without any file - for the test bed.</summary>
		public static SecuritySettings WithSecret( string secret, ESecurityMode mode = ESecurityMode.On )
		{
			if( mode == ESecurityMode.Off ) return Off;
			return new SecuritySettings( mode, ValidateSecret( secret, "(in memory)" ), "(in memory)", null );
		}

		public override string ToString() => $"Security={Mode}, secret from {SiteSecretSource}";

		/// <summary>Parses off|audit|on, any case.</summary>
		/// <exception cref="SecurityConfigException">anything else - a typo must not silently mean "off"</exception>
		public static ESecurityMode ParseMode( string? text )
		{
			var t = ( text ?? string.Empty ).Trim();
			if( t.Length == 0 ) return ESecurityMode.Off;
			if( t.Equals( "off", StringComparison.OrdinalIgnoreCase ) ) return ESecurityMode.Off;
			if( t.Equals( "audit", StringComparison.OrdinalIgnoreCase ) ) return ESecurityMode.Audit;
			if( t.Equals( "on", StringComparison.OrdinalIgnoreCase ) ) return ESecurityMode.On;
			throw new SecurityConfigException( $"Security '{text}' is not one of: off, audit, on." );
		}

		/// <summary>
		/// Loads what the mode needs. With Off nothing is read. With Audit or On the site secret must be found and valid, or this throws.
		/// </summary>
		/// <param name="explicitSecretFile">--siteSecretFile or the setting: if given, the only place looked at</param>
		/// <param name="explicitTokenKeyFile">--tokenPublicKeyFile or the setting</param>
		/// <param name="localConfigFile">the LocalConfig.xml in use, whose folder is looked in next; empty if none</param>
		/// <param name="exeFolder">the folder of the executable, the last place</param>
		public static SecuritySettings Load( ESecurityMode mode, string? explicitSecretFile, string? explicitTokenKeyFile, string? localConfigFile, string exeFolder, bool needTokenKey = false, string? tokenAudience = null )
		{
			if( mode == ESecurityMode.Off ) return Off;

			var secretPath = Locate( SiteSecretFileName, "--siteSecretFile", explicitSecretFile, localConfigFile, exeFolder, "the site secret" );
			var secret = ValidateSecret( ReadSecretText( secretPath ), secretPath );

			// the key that checks REST tokens: only looked for now, a later step needs it; the master port does not
			string? keyPath = null;
			if( !string.IsNullOrWhiteSpace( explicitTokenKeyFile ) )
				keyPath = Locate( TokenPublicKeyFileName, "--tokenPublicKeyFile", explicitTokenKeyFile, localConfigFile, exeFolder, "the token public key" );
			else
				keyPath = TryLocate( TokenPublicKeyFileName, localConfigFile, exeFolder );

			// the master that runs a web server checks its tokens against the key: no key, no start - never an open API by accident
			TokenValidator? tokens = null;
			if( needTokenKey )
			{
				keyPath ??= Locate( TokenPublicKeyFileName, "--tokenPublicKeyFile", null, localConfigFile, exeFolder, "the token public key (the web API needs it while security is on)" );
				string pem;
				try { pem = File.ReadAllText( keyPath ); }
				catch( Exception ex ) { throw new SecurityConfigException( $"The token public key file '{keyPath}' could not be read: {ex.Message}" ); }
				try { tokens = new TokenValidator( pem, tokenAudience ); }
				catch( SecurityConfigException ex ) { throw new SecurityConfigException( $"{ex.Message} (file '{keyPath}')" ); }
			}

			return new SecuritySettings( mode, secret, secretPath, keyPath ) { Tokens = tokens, _tokenAudience = string.IsNullOrWhiteSpace( tokenAudience ) ? null : tokenAudience.Trim() };
		}

		/// <summary>The places a file is looked for, in order, as (path, what it is).</summary>
		public static List<(string Path, string Origin)> SearchPlaces( string fileName, string optionName, string? explicitPath, string? localConfigFile, string exeFolder )
		{
			var places = new List<(string, string)>();

			if( !string.IsNullOrWhiteSpace( explicitPath ) )
			{
				// given by name: that file or none. A secret taken from somewhere else than where it was pointed to would be a surprise
				places.Add( ( Path.GetFullPath( explicitPath ), $"given by {optionName} or its setting" ) );
				return places;
			}

			if( !string.IsNullOrWhiteSpace( localConfigFile ) )
			{
				var dir = Path.GetDirectoryName( Path.GetFullPath( localConfigFile ) );
				if( !string.IsNullOrEmpty( dir ) )
					places.Add( ( Path.Combine( dir, fileName ), $"the folder of the LocalConfig.xml in use ({localConfigFile})" ) );
			}

			places.Add( ( Path.Combine( Path.GetFullPath( exeFolder ), fileName ), "the folder of the executable" ) );
			return places;
		}

		static string? TryLocate( string fileName, string? localConfigFile, string exeFolder )
			=> SearchPlaces( fileName, "", null, localConfigFile, exeFolder ).Select( p => p.Path ).FirstOrDefault( File.Exists );

		static string Locate( string fileName, string optionName, string? explicitPath, string? localConfigFile, string exeFolder, string what )
		{
			var places = SearchPlaces( fileName, optionName, explicitPath, localConfigFile, exeFolder );
			foreach( var (path, _) in places )
				if( File.Exists( path ) ) return path;

			var sb = new StringBuilder();
			sb.Append( $"Security is on but {what} ('{fileName}') was not found. Looked in:" );
			foreach( var (path, origin) in places )
				sb.Append( $"\n  {path}  - {origin}" );
			if( string.IsNullOrWhiteSpace( explicitPath ) && string.IsNullOrWhiteSpace( localConfigFile ) )
				sb.Append( "\n  (no LocalConfig.xml is in use, so its folder was not searched)" );
			sb.Append( $"\nPut the file in one of those places, or name it with {optionName}; or set Security=off. See docs/Security.md." );
			throw new SecurityConfigException( sb.ToString() );
		}

		static string ReadSecretText( string path )
		{
			try { return File.ReadAllText( path, Encoding.UTF8 ); }
			catch( Exception ex ) { throw new SecurityConfigException( $"The site secret file '{path}' could not be read: {ex.Message}" ); }
		}

		static byte[] ValidateSecret( string? text, string source )
		{
			var trimmed = ( text ?? string.Empty ).Trim();
			if( trimmed.Length < MinSecretLength )
				throw new SecurityConfigException( $"The site secret in '{source}' is {trimmed.Length} characters long; at least {MinSecretLength} are required (64 random hex characters are suggested - see docs/Security.md)." );
			return Encoding.UTF8.GetBytes( trimmed );
		}
	}

	/// <summary>
	/// The <c>CLIPort</c> setting: a port number, 0 for "the default", or <c>off</c> for "no telnet CLI at all".
	/// </summary>
	public static class CliPortSetting
	{
		public enum EKind
		{
			/// <summary>Not said: the default port, or no CLI at all when security is on.</summary>
			Default,
			Port,
			Off,
		}

		/// <summary>Reads the text of the option or setting.</summary>
		/// <returns>false if it is neither a number from 0 to 65535 nor "off"</returns>
		public static bool TryParse( string? text, out EKind kind, out int port )
		{
			kind = EKind.Default;
			port = 0;

			var t = ( text ?? string.Empty ).Trim();
			if( t.Length == 0 ) return true;

			if( t.Equals( "off", StringComparison.OrdinalIgnoreCase ) )
			{
				kind = EKind.Off;
				return true;
			}

			if( int.TryParse( t, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n ) && n >= 0 && n <= 65535 )
			{
				if( n == 0 ) return true; // as before: 0 means the default
				kind = EKind.Port;
				port = n;
				return true;
			}

			return false;
		}
	}
}
