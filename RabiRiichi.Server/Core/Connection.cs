using RabiRiichi.Communication.Json;
using RabiRiichi.Server.Models;
using RabiRiichi.Util;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Server.Core {
    public class Connection : IDisposable {
        protected class Message {
            [JsonIgnore] public AtomicBool isQueued;
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
        /// Message queue (unique for each websocket)
        /// </summary>
        protected BlockingCollection<Message> msgQueue;
        protected CancellationTokenSource sendCts;

        protected readonly User user;
        protected readonly int playerId;
        /// <summary>
        /// Last message ID. Shared by all connections with the same player.
        /// </summary>
        public readonly AutoIncrementInt lastMsgId = new();
        /// <summary>
        /// Mapping from message ID to message. Shared by all connections with the same player.
        /// </summary>
        protected readonly ConcurrentDictionary<int, Message> msgLookup = new();

        /// <summary>
        /// Callback when a message is received.
        /// </summary>
        public Action<WebSocket, JsonElement> OnReceive;
        /// <summary>
        /// Callback when the player is disconnected.
        /// </summary>
        public Action<WebSocket> OnDisconnect;
        /// <summary>
        /// Whether the connection is closed.<br/>
        /// If true, no more web sockets or connections are accepted.
        /// </summary>
        public AtomicBool isClosed = new();

        public Connection(User user) {
            this.user = user;
            this.playerId = user.room.players.IndexOf(user);
        }

        #region Send message
        protected async Task SendMsgLoop(WebSocket ws) {
            var queue = msgQueue;
            var cts = sendCts;
            while (true) {
                try {
                    var msg = queue.Take(cts.Token);
                    msg.isQueued.Set(false);
                    var jsonStr = RabiJson.Stringify(msg, playerId);
                    await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonStr)),
                        WebSocketMessageType.Text, true, cts.Token);
                } catch (OperationCanceledException) {
                    // Cancellation token cancelled. Could be:
                    // 1. A new ws connection coming in
                    // 2. Client informed that they closed the connection
                    break;
                } catch (InvalidOperationException) {
                    // Cannot take from queue anymore.
                    // The upper layer has specified that no more message is being sent,
                    // so we can safely close the connection. No further connection will be made.
                    break;
                }
            }
            // Closed connection
            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            // Notify receive loop to stop
            if (Interlocked.CompareExchange(ref sendCts, null, cts) == cts) {
                cts.Cancel();
                cts.Dispose();
            }
            // Invoke callback. This will be called once and only once.
            OnDisconnect?.Invoke(ws);
        }

        protected bool Requeue(int id) {
            if (msgLookup.TryGetValue(id, out var msg)) {
                return msg.TryQueue(msgQueue);
            }
            return false;
        }

        /// <summary>
        /// Add a message to queue. It will not be sent immediately.
        /// </summary>
        public void Queue(object msg) {
            if (isClosed) {
                throw new InvalidOperationException("Connection is closed");
            }
            try {
                msgQueue.Add(new Message {
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
        public void Connect(WebSocket ws) {
            if (isClosed) {
                throw new InvalidOperationException("Connection is closed");
            }
            // Cancel previous connection
            var oldCts = Interlocked.Exchange(ref sendCts, new CancellationTokenSource());
            if (oldCts != null) {
                oldCts.Cancel();
                oldCts.Dispose();
            }

            // Use new message queue and message loops
            msgQueue = new BlockingCollection<Message>();
            Task.Run(() => SendMsgLoop(ws));
            Task.Run(() => ReceiveMsgLoop(ws));
        }
        #endregion

        #region Receive Msg

        protected async Task ReceiveMsgLoop(WebSocket ws) {
            var cts = sendCts;
            var byteArr = new ArraySegment<byte>(new byte[1024]);
            while (true) {
                try {
                    var msg = await ws.ReceiveAsync(byteArr, cts.Token);
                    if (!msg.EndOfMessage) {
                        // Too long, refuse to parse
                        while (!(await ws.ReceiveAsync(byteArr, cts.Token)).EndOfMessage) { }
                        continue;
                    }
                    if (msg.MessageType == WebSocketMessageType.Close) {
                        // Client closes connection, notify send loop to close
                        break;
                    } else if (msg.MessageType == WebSocketMessageType.Text) {
                        var jsonStr = Encoding.UTF8.GetString(byteArr.Array, 0, msg.Count);
                        var json = JsonSerializer.Deserialize<JsonElement>(jsonStr);
                        OnReceive?.Invoke(ws, json);
                    }
                } catch (OperationCanceledException) {
                    // Cancellation token cancelled. Could be:
                    // 1. A new ws connection coming in
                    // 2. Client informed that they closed the connection
                    return;
                } catch (ArgumentException) {
                    // Invalid message
                    continue;
                } catch (JsonException) {
                    // Invalid message
                    continue;
                }
            }
            // Client closed connection, notify send loop to close
            if (Interlocked.CompareExchange(ref sendCts, null, cts) == cts) {
                cts.Cancel();
                cts.Dispose();
            }
        }
        #endregion

        #region Disconnect & Dispose

        /// <summary>
        /// Closes the connection. <br/>No more web sockets or messages are accepted,
        /// and the queued messages will be sent before closing.
        /// </summary>
        public void Close() {
            if (isClosed.Exchange(true)) {
                return;
            }
            msgQueue?.CompleteAdding();
        }

        protected void Dispose(bool disposing) {
            if (disposing) {
                var cts = Interlocked.Exchange(ref sendCts, null);
                if (cts != null) {
                    cts.Cancel();
                    cts.Dispose();
                }
            }
        }

        /// <summary>
        /// Implemented for IDisposable.
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}