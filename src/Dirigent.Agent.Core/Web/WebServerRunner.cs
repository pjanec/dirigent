using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Actions;
using EmbedIO.Files;
using EmbedIO.Security;
using EmbedIO.WebApi;
//using Swan;
//using Swan.Logging;

namespace Dirigent.Web
{
	public static class WebServerRunner
	{
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        //private const bool OpenBrowser = true;
        private const bool UseFileCache = true;

        //private static void Main(string[] args)
        //{
        //    var url = args.Length > 0 ? args[0] : "http://*:8877";

        //    using (var cts = new CancellationTokenSource())
        //    {
        //        Task.WaitAll(
        //            RunWebServerAsync(url, HtmlRootPath, cts.Token) );
        //    }
        //}

        // Gets the local path of shared files.
        // When debugging, take them directly from source so we can edit and reload.
        // Otherwise, take them from the deployment directory.
        public static string HtmlRootPath
        {
            get
            {
                var assemblyPath = Path.GetDirectoryName(typeof(WebServerRunner).Assembly.Location) ?? Directory.GetCurrentDirectory();

#if DEBUG
                return Path.Combine(Directory.GetParent(assemblyPath)?.Parent?.Parent?.Parent?.Parent?.FullName ?? ".", "html");
#else
                return Path.Combine(assemblyPath, "html");
#endif
            }
        }

        /// <summary>Every error of the API is a small JSON document, not EmbedIO's HTML page.</summary>
        static Task JsonHttpError( IHttpContext context, IHttpException exception )
        {
            var code = exception.StatusCode;
            var message = string.IsNullOrEmpty( exception.Message )
                            ? ( (System.Net.HttpStatusCode) code ).ToString()
                            : exception.Message;
            return SendJsonError( context, code, message );
        }

        /// <summary>More requests than this from one address within a second ban it - see <see cref="BanMinutes"/>.</summary>
        public const int MaxRequestsPerSecond = 50;

        /// <summary>How long a banned address is answered 403.</summary>
        public const int BanMinutes = 10;

        static Task ServerHttpError( IHttpContext context, IHttpException exception )
        {
            if( !context.Request.Url.AbsolutePath.StartsWith( "/api", StringComparison.OrdinalIgnoreCase ) )
                return HttpExceptionHandler.Default( context, exception );

            // the ban says nothing of itself; the caller needs to know what happened and for how long
            if( exception.StatusCode == 403 && string.IsNullOrEmpty( exception.Message ) )
                return SendJsonError( context, 403, $"This address sent more than {MaxRequestsPerSecond} requests in a second and is"
                    + $" banned for {BanMinutes} minutes. Ask for lists (GET /api/appstates, /api/planstates, ...) rather than one item at a time." );

            return JsonHttpError( context, exception );
        }

        /// <summary>
        /// What a route returns, as JSON - System.Text.Json, like the answers the file and download routes compose themselves.
        /// EmbedIO's own serializer wrote a nullable number as a string and left line breaks in strings raw.
        /// </summary>
        static Task JsonResponse( IHttpContext context, object? data )
            => context.SendStringAsync( System.Text.Json.JsonSerializer.Serialize( data, data?.GetType() ?? typeof( object ) ),
                    "application/json", ApiOps.Utf8 );

        static Task JsonUnhandledError( IHttpContext context, Exception exception )
            => SendJsonError( context, 500, Tools.JustFirstLine( exception.Message ) );

        static Task SendJsonError( IHttpContext context, int code, string message )
        {
            context.Response.StatusCode = code;
            var json = System.Text.Json.JsonSerializer.Serialize( new { error = message, code } );
            return context.SendStringAsync( json, "application/json", ApiOps.Utf8 );
        }

        // Create and configure our web server.
        private static WebServer CreateWebServer( string url, string htmlRootPath, Master master )
        {
#pragma warning disable CA2000 // Call Dispose on object - this is a factory method.
            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                // Only the request rate bans. There used to be a rule banning an address after a few
                // "HTTP exception 404" log lines, but that criterion matches log text and cannot be limited to
                // the static files: a tool polling a download that has been cleaned up, or asking for an
                // app that is not there, got itself banned (403 for ten minutes) by answers that are
                // perfectly normal for an API.
                .WithIPBanning(o => o
                    .WithMaxRequestsPerSecond(MaxRequestsPerSecond), banMinutes: BanMinutes)
                .WithLocalSessionManager()
                //.WithCors(
                //    "http://unosquare.github.io,http://run.plnkr.co", // Origins, separated by comma without last slash
                //    "content-type, accept", // Allowed headers
                //    "post") // Allowed methods
                //.WithWebApi("/api", m => m
                //    .WithController<PeopleController>())
                //.WithModule(new WebSocketChatModule("/chat"))
                //.WithModule(new WebSocketTerminalModule("/terminal"))


                .WithWebApi("/api", JsonResponse, m => m
                    .WithController( () => new PlanApiController( master ) )
                    .WithController( () => new AppApiController( master ) )
                    .WithController( () => new CrashApiController( master ) )
                    .WithController( () => new DownloadApiController( master ) )
                    .WithController( () => new HelpApiController() )
                    .WithController( () => new MarkApiController( master ) )
                    .WithController( () => new ScriptApiController( master ) )
                    .WithController( () => new CmdApiController( master ) )
                    .WithController( () => new ClientApiController( master ) )
                    .WithController( () => new MachineApiController( master ) )
                    .WithController( () => new ControlApiController( master ) )
                    .WithController( () => new IndexApiController( master ) )
                    .HandleHttpException( JsonHttpError )
                    .HandleUnhandledException( JsonUnhandledError )
                )

                .WithModule(new WebSocketDirigentModule("/websock"))

                // The root leads to the API's own description, GET /api - what a tool or an agent that knows only the
                // address asks first. Only "/" itself: any other path is still served from the html folder, should a
                // web page be put there again (the one there was spoke to routes that no longer exist).
                .WithModule(new EmbedIO.Actions.RedirectModule("/", "/api",
                    ctx => ctx.RequestedPath == "/" && ctx.Request.HttpVerb == HttpVerbs.Get))

                .WithStaticFolder("/", htmlRootPath, true, m => m
                    .WithContentCaching(UseFileCache)) // Add static files after other modules to avoid conflicts
                //.WithModule(new ActionModule("/", HttpVerb.Any, ctx => ctx.SendDataAsync(new { Message = "Error" })))
                ;

            // What is answered before any module - the ban above, a path no module takes (".." in it) - comes here.
            // Under /api it is the JSON of every other API error; the static page keeps EmbedIO's HTML.
            server.HandleHttpException( ServerHttpError );

            // Listen for state changes.
            server.StateChanged += (s, e) => log.Debug( $"WebServer New State - {e.NewState}" );

            return server;
#pragma warning restore CA2000
        }

        // Create and run a web server.
        public static async Task RunWebServerAsync( Master master, string url, string htmlRootPath, CancellationToken cancellationToken)
        {
            using var server = CreateWebServer(url, htmlRootPath, master );
            await server.RunAsync(cancellationToken).ConfigureAwait(false);
        }

        // Open the default browser on the web server's home page.
#pragma warning disable CA1801 // Unused parameter
        public static async Task ShowBrowserAsync(string url, CancellationToken cancellationToken)
#pragma warning restore CA1801
        {
            // Be sure to run in parallel.
            await Task.Yield();

            // Fire up the browser to show the content!
            using var browser = new Process
            {
                StartInfo = new ProcessStartInfo(url)
                {
                    UseShellExecute = true,
                },
            };
            browser.Start();
        }

	}
}
