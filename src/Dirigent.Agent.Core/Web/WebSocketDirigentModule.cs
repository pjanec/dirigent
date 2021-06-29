using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO.WebSockets;
using Swan.Logging;

namespace Dirigent.Web
{
    /// <summary>
    /// Periodic notifications about app/plan state from the dirigent master
    /// </summary>
    public class WebSocketDirigentModule : WebSocketModule
    {
        CancellationTokenSource _taskCancelSrc;
        Task _task;

        public class PlanStateNotifMessage
        {
            public string type => "planState";
            public PlanState data {get; set;}
            public PlanStateNotifMessage( PlanState data )
            {
                this.data = data;
            }
        }

        public WebSocketDirigentModule(string urlPath)
            : base(urlPath, true)
        {
            _taskCancelSrc = new CancellationTokenSource();
            _task = Task.Run(() =>
                {
                    SimulatePeriodicalStatusPush( _taskCancelSrc.Token );
                }
             );
             _task.ConfigureAwait(false);
        }


        async void SimulatePeriodicalStatusPush( CancellationToken token )
        {
            while( !token.IsCancellationRequested )
            {

                var msg = new PlanStateNotifMessage( 
                    new PlanState
                    {
                        id = "plan1",
                        state = new PlanStateDetails
                        {
                            code = "InProgress"
                        }
                    }
                );
                var jsonMsg =Swan.Formatters.Json.Serialize(msg);

                $"Simulating the sending of status... {jsonMsg}".Info();
                await BroadcastAsync( jsonMsg );


                await Task.Delay(2000).ConfigureAwait(false); // false=no need to return to same thread
            }
            "Finished looping...".Info();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if( !disposing ) return;

            "Cancelling the looping...".Info();
            _taskCancelSrc.Cancel();
            "Waiting to finish...".Info();
            _task.Wait();
            "Wait finished.".Info();
        }



        /// <inheritdoc />
        protected override Task OnMessageReceivedAsync(
            IWebSocketContext context,
            byte[] buffer,
            IWebSocketReceiveResult result)
        {
            return Task.CompletedTask;
            //return SendToOthersAsync(context, Encoding.GetString(buffer));
        }

        /// <inheritdoc />
        protected override Task OnClientConnectedAsync(IWebSocketContext context)
        {
            return Task.CompletedTask;
        }

            //=> Task.WhenAll(
            //    //SendAsync(context, "Welcome to the chat room!"),
            //    SendToOthersAsync(context, "Someone joined."));

        /// <inheritdoc />
        protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
        {
            return Task.CompletedTask;
            //=> SendToOthersAsync(context, "Someone left.");
        }

        //private Task SendToOthersAsync(IWebSocketContext context, string payload)
        //    => BroadcastAsync(payload, c => c != context);

    }
}