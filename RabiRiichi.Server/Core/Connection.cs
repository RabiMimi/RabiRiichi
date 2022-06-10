using RabiRiichi.Communication.Json;
using RabiRiichi.Server.Models;
using RabiRiichi.Util;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Server.Core {
    public class Connection : IDisposable {
        /// <summary>
        /// Message structure only used in WS Context.
        /// </summary>
        internal class Message {
            [JsonIgnore] public readonly AtomicBool isQueued = new();
            public bool TryQueue(BlockingCollection<Message> queue) {
                if (isQueued.Exchange(true)) {
                    return false;
                }
                queue.Add(this);
                return true;
            }

            public int id;
            public object message;
        }

        /// <summary>
        /// HeartBeat message. Should always have id = -1.
        /// </summary>
        protected class HeartBeatMessage {
            public string msgType => "hb";
            public int evId { get; init; }

            public HeartBeatMessage(int evId) {
                this.evId = evId;
            }
        }

        /// <summary>
        /// Current WebSocket context. Null if not connected.<br/>
        /// Will not be reset to null when the connection is closed.
        /// </summary>
        protected RabiWSContext currentCtx;

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
        public readonly AutoIncrementInt lastMsgId = new();

        /// <summary>
        /// Mapping from message ID to message. Shared by all connections with the same player.
        /// </summary>
        internal readonly ConcurrentDictionary<int, Message> msgLookup = new();

        /// <summary>
        /// Whether the connection is closed.<br/>
        /// If true, no more web sockets or connections are accepted.
        /// </summary>
        public readonly AtomicBool isClosed = new();

        public Connection(User user) {
            this.user = user;
            this.playerId = user.room.players.IndexOf(user);
        }

        /// <summary>
        /// Add a message to queue. It will not be sent immediately.
        /// </summary>
        public void Queue<T>(T msg) {
            if (isClosed) {
                return;
            }
            try {
                currentCtx?.Queue(new Message {
                    id = lastMsgId.Next,
                    message = msg,
                });
            } catch (InvalidOperationException) {
                // No more message can be queued.
            }
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
                ctx.Queue(new Message {
                    id = -1, // Heartbeat does not need ID
                    message = new HeartBeatMessage(lastMsgId.Value),
                });
            }
        }

        /// <summary>
        /// Receives a heartbeat package.
        /// If no response is received in 15 seconds, the connection will be closed.
        /// </summary>
        private void HeartBeatRecvLoop(RabiWSContext ctx) {
            var received = new ManualResetEvent(false);
            ctx.OnReceive += (JsonElement json) => {
                received.Set();
                try {
                    // Client sends heart beat requesting an event to be resent.
                    if (json.TryGetProperty("msgType", out var msgType)
                        && msgType.GetString() == "hb"
                        && json.TryGetProperty("evId", out var evId)
                        && evId.TryGetInt32(out var evIdInt)
                        && evIdInt > 0
                        && msgLookup.TryGetValue(evIdInt, out var msg)) {
                        ctx.Queue(msg);
                    }
                } catch (InvalidOperationException) {
                    // Invalid msgType, ignore
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