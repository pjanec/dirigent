using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;

namespace Dirigent.Web
{
	/// <summary>
	/// The documentation, served by the master that runs it: every master carries the pages of its own
	/// version.
	/// </summary>
	/// <remarks>
	/// The pages are embedded in the assembly (the docs folder, minus the design notes). Their relative
	/// links - <c>Files.md#x</c> - resolve against <c>/api/help/HTTP.md</c> to <c>/api/help/Files.md#x</c>, which
	/// is why <c>/api/help</c> redirects to the page rather than serving it in place.
	/// </remarks>
	public class HelpApiController : WebApiController
	{
		const string Prefix = "docs/";
		const string StartPage = "HTTP.md";

		static readonly Assembly _asm = typeof( HelpApiController ).Assembly;

		/// <summary>The names of the embedded pages, as they are served.</summary>
		public static string[] Pages
			=> _asm.GetManifestResourceNames()
					.Where( n => n.StartsWith( Prefix, StringComparison.Ordinal ) )
					.Select( n => n.Substring( Prefix.Length ) )
					.OrderBy( n => n, StringComparer.OrdinalIgnoreCase )
					.ToArray();

		// the start page, as /api/help/HTTP.md so that its relative links lead to the other pages
		[Scope( ScopeAttribute.Open )]
		[Route( HttpVerbs.Get, "/help" )]
		public Task GetHelp()
		{
			HttpContext.Redirect( "/api/help/" + StartPage, (int) System.Net.HttpStatusCode.Found );
			return Task.CompletedTask;
		}

		// one page of the documentation, by file name (case does not matter)
		[Scope( ScopeAttribute.Open )]
		[Route( HttpVerbs.Get, "/help/{doc}" )]
		public async Task GetHelpPage( string doc )
		{
			// only the embedded set: a name with anything path-like in it is never one of them
			var page = Pages.FirstOrDefault( p => string.Equals( p, doc, StringComparison.OrdinalIgnoreCase ) );
			if( page is null )
				throw HttpException.NotFound( $"There is no documentation page '{doc}'." );

			await HttpContext.SendStringAsync( ReadPage( page )!, "text/markdown", ApiOps.Utf8 );
		}

		/// <summary>The text of an embedded page, null if there is none of that name.</summary>
		public static string? ReadPage( string page )
		{
			using var stream = _asm.GetManifestResourceStream( Prefix + page );
			if( stream is null ) return null;
			using var reader = new StreamReader( stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
			return reader.ReadToEnd();
		}
	}
}
