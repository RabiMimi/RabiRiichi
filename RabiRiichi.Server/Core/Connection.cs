using RabiRiichi.Server.Messages;
using RabiRiichi.Server.Models;
using RabiRiichi.Util;
using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace RabiRiichi.Server.Core {
    public class Connection : IDisposable {
        /// <summary>
        /// Current WebSocket context. Null if not connected.<br/>
        /// Will not be reset to null when the connection is closed.
        /// </summary>
        protected RabiWSContext currentCtx;

        /// <summary>
        /// Callback when a message is received.
        /// </summary>
        public Action<InMessage> OnReceive;

        /// <summary>
        /// Switch to a new WS context. Old connection will be closed.
        /// </summary>
        protected void SwitchWSContext(RabiWSContext newCts) {
            Interlocked.Exchange(ref currentCtx, newCts)?.cts.Cancel();
        }

        protected readonly User user;
        protected readonly int playerId;

        /// <summary>
        /// Last message ID. Shared by all connections with the same player.
        /// </summary>
        public readonly AutoIncrementInt msgId = new();

        /// <summary>
        /// Mapping from message ID to message. Shared by all connections with the same player.
        /// </summary>
        internal readonly ConcurrentDictionary<int, OutMessage> msgLookup = new();

        /// <summary>
        /// Whether the connection is closed.<br/>
        /// If true, no more web sockets or connections are accepted.
        /// </summary>
        public readonly AtomicBool isClosed = new();

        public Connection(User user) {
            this.user = user;
            this.playerId = user.playerId;
        }

        /// <summary>
        /// Add a message to queue. It will not be sent immediately.
        /// </summary>
        /// <returns>The message ID. -1 if connection closed.</returns>
        public int Queue<T>(string type, T msg) {
            if (isClosed) {
                return -1;
            }
            var message = new OutMessage(msgId.Next, type, msg);
            try {
                currentCtx?.Queue(message);
            } catch (InvalidOperationException) {
                // No more message can be queued.
            }
            return message.id;
        }

        /// <summary>
        /// Connects to a websocket. Existing connection will be closed.
        /// </summary>
        public RabiWSContext Connect(WebSocket ws) {
            if (isClosed) {
                throw new InvalidOperationException("Connection is closed");
            }

            var ctx = new RabiWSContext(ws, playerId);
            // Start message loops before context switch
            ctx.RunLoops(
                HeartBeatLoop(ctx),
                Task.Run(() => HeartBeatRecvLoop(ctx))
            ).ConfigureAwait(false);

            // Cancel previous connection and switch context
            SwitchWSContext(ctx);

            return ctx;
        }

        /// <summary>
        /// Sends a heartbeat package every 5 seconds.
        /// </summary>
        private async Task HeartBeatLoop(RabiWSContext ctx) {
            while (true) {
                try {
                    await Task.Delay(TimeSpan.FromSeconds(5), ctx.cts.Token);
                } catch (OperationCanceledException) {
                    break;
                }
                // Get a list of missing events
                int nextReceiveMsgId = ctx.maxReceivedMsgId + 1;
                int maxClientMsgId = ctx.maxClientMsgId;
                int count = Math.Min(16, maxClientMsgId - nextReceiveMsgId);
                List<int> requestingEvents = count > 0
                    ? new(Enumerable.Range(nextReceiveMsgId, count)) : null;
                ctx.Queue(new OutMessage(-1, OutMsgType.HeartBeat,
                    InOutHeartBeat.From(msgId.Value, requestingEvents)));
            }
        }

        /// <summary>
        /// Receives a heartbeat package.
        /// If no response is received in 15 seconds, the connection will be closed.
        /// </summary>
        private void HeartBeatRecvLoop(RabiWSContext ctx) {
            var received = new ManualResetEvent(false);
            ctx.OnReceive += (InMessage incoming) => {
                // Always resets heartbeat timer even if the message is not a heartbeat
                received.Set();
                OnReceive?.Invoke(incoming);
                if (!incoming.TryGetMessage<InOutHeartBeat>(out var heartBeat)) {
                    return;
                }
                // Update maximum client message ID
                ctx.maxClientMsgId = heartBeat.maxMsgId;
                if (heartBeat.requestingEvents == null) {
                    return;
                }
                // Client requesting events to be resent
                foreach (var evt in heartBeat.requestingEvents) {
                    if (msgLookup.TryGetValue(evt, out var msg)) {
                        ctx.Queue(msg);
                    }
                }
            };
            while (true) {
                received.Reset();
                int index = WaitHandle.WaitAny(
                    new WaitHandle[] { received, ctx.cts.Token.WaitHandle },
                    TimeSpan.FromSeconds(15));
                if (index == 0) {
                    // Received a message from client
                    continue;
                } else if (index == 1) {
                    // Closed connection
                    break;
                } else {
                    // No response from client, close connection
                    ctx.cts.Cancel();
                    break;
                }
            }
        }

        /// <summary>
        /// Closes the connection. <br/>No more web sockets or messages are accepted,
        /// and the queued messages will be sent before closing.
        /// </summary>
        public void Close() {
            if (isClosed.Exchange(true)) {
                return;
            }
            currentCtx?.Close();
        }

        protected void Dispose(bool disposing) {
            if (disposing) {
                SwitchWSContext(null);
            }
        }

        /// <summary>
        /// Implemented for IDisposable.
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}