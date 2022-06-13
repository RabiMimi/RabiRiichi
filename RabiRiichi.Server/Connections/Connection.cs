using RabiRiichi.Server.Messages;
using RabiRiichi.Server.Models;
using RabiRiichi.Util;
using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace RabiRiichi.Server.Utils {
    public class Connection : IDisposable {
        /// <summary>
        /// Current WebSocket context. Null if not connected.<br/>
        /// Will not be reset to null when the connection is closed.
        /// </summary>
        protected RabiWSContext currentCtx;
        public RabiWSContext Current => currentCtx;

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
        /// Create an outgoing message but do not send it.
        /// </summary>
        public OutMessage CreateMessage<T>(string type, T msg, int? respondTo = null)
            => new(msgId.Next, type, msg, respondTo);

        /// <summary>
        /// Add a message to queue. It will not be sent immediately.
        /// </summary>
        /// <param name="msg">Message to send</param>
        /// <returns>Whether the message was added to queue.</returns>
        public bool Queue(OutMessage msg) {
            if (isClosed) {
                return false;
            }
            try {
                currentCtx?.Queue(msg);
            } catch (InvalidOperationException) {
                // No more message can be queued.
            }
            return true;
        }

        /// <summary>
        /// Add a message to queue. It will not be sent immediately.
        /// </summary>
        public OutMessage Queue<T>(string type, T msg) {
            var ret = CreateMessage(type, msg);
            return Queue(ret) ? ret : null;
        }

        /// <summary>
        /// Connects to a websocket. Existing connection will be closed.
        /// </summary>
        public RabiWSContext Connect(WebSocket ws) {
            if (isClosed) {
                throw new InvalidOperationException("Connection is closed");
            }

            var ctx = new RabiWSContext(this, ws, playerId);
            // Start message loops before context switch
            ctx.RunLoops(
                Task.Run(() => HeartBeatRecvLoop(ctx))
            ).ConfigureAwait(false);

            // Cancel previous connection and switch context
            SwitchWSContext(ctx);

            return ctx;
        }

        /// <summary>
        /// When a heartbeat arrives, updates relevant fields and replies.
        /// </summary>
        private void OnReceiveHeartBeat(RabiWSContext ctx, InOutHeartBeat msg, int respondTo) {
            // Update maximum client message ID
            ctx.maxClientMsgId = msg.maxMsgId;
            // Respond to the heartbeat
            int maxReceivedMsgId = ctx.maxReceivedMsgId;
            int count = Math.Min(16, msg.maxMsgId - maxReceivedMsgId);
            List<int> requestingEvents = count > 0
                ? new(Enumerable.Range(maxReceivedMsgId + 1, count)) : null;
            ctx.Queue(new OutMessage(-1, OutMsgType.HeartBeat,
                InOutHeartBeat.From(msgId.Value, requestingEvents), respondTo));
            if (msg.requestingEvents == null) {
                return;
            }
            // Client requesting events to be resent
            foreach (var evt in msg.requestingEvents) {
                if (msgLookup.TryGetValue(evt, out var oldMsg)) {
                    ctx.Queue(oldMsg);
                }
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
                // Check if the message is responding to a server message
                if (msgLookup.TryGetValue(incoming.respondTo, out var msg)) {
                    msg.responseTcs.TrySetResult(incoming);
                }
                if (incoming.TryGetMessage<InOutHeartBeat>(out var heartBeat)) {
                    OnReceiveHeartBeat(ctx, heartBeat, msg.id);
                }
            };
            while (true) {
                received.Reset();
                int index = WaitHandle.WaitAny(
                    new WaitHandle[] { received, ctx.cts.Token.WaitHandle },
                    ServerConstants.RESPONSE_TIMEOUT);
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