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

        private static void Main(string[] args)
        {
            var url = args.Length > 0 ? args[0] : "http://*:8877";

            using (var cts = new CancellationTokenSource())
            {
                Task.WaitAll(
                    RunWebServerAsync(url, HtmlRootPath, cts.Token) );
            }
        }

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

        // Create and configure our web server.
        private static WebServer CreateWebServer(string url, string htmlRootPath)
        {
#pragma warning disable CA2000 // Call Dispose on object - this is a factory method.
            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                .WithIPBanning(o => o
                    .WithMaxRequestsPerSecond()
                    .WithRegexRules("HTTP exception 404"))
                .WithLocalSessionManager()
                //.WithCors(
                //    "http://unosquare.github.io,http://run.plnkr.co", // Origins, separated by comma without last slash
                //    "content-type, accept", // Allowed headers
                //    "post") // Allowed methods
                //.WithWebApi("/api", m => m
                //    .WithController<PeopleController>())
                //.WithModule(new WebSocketChatModule("/chat"))
                //.WithModule(new WebSocketTerminalModule("/terminal"))


                .WithWebApi("/api", m => m
                    .WithController<PlansApiController>()
                    .WithController<AppsApiController>()
                    .WithController<CmdApiController>()
                  )

                .WithModule(new WebSocketDirigentModule("/websock"))

                .WithStaticFolder("/", htmlRootPath, true, m => m
                    .WithContentCaching(UseFileCache)) // Add static files after other modules to avoid conflicts
                //.WithModule(new ActionModule("/", HttpVerb.Any, ctx => ctx.SendDataAsync(new { Message = "Error" })))
                ;

            // Listen for state changes.
            server.StateChanged += (s, e) => log.Debug( $"WebServer New State - {e.NewState}" );

            return server;
#pragma warning restore CA2000
        }

        // Create and run a web server.
        public static async Task RunWebServerAsync(string url, string htmlRootPath, CancellationToken cancellationToken)
        {
            using var server = CreateWebServer(url, htmlRootPath);
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
