using RabiRiichi.Communication.Json;
using RabiRiichi.Server.Models;
using RabiRiichi.Util;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
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
        protected readonly BlockingCollection<Message> msgQueue = new();
        protected readonly ConcurrentDictionary<int, Message> msgLookup = new();
        protected readonly CancellationTokenSource cts = new();

        protected WebSocket _ws;
        protected WebSocket ws {
            get => _ws;
            set {
                if (Interlocked.Exchange(ref _ws, value) == null) {
                    Task.Run(SendMsgLoop);
                    Task.Run(ReceiveMsgLoop);
                }
            }
        }
        public readonly User user;
        public readonly int playerId;
        public readonly AutoIncrementInt lastMsgId = new();

        public Connection(User user) {
            this.user = user;
            this.playerId = user.room.players.IndexOf(user);
        }

        #region Send message
        protected async Task SendMsgLoop() {
            while (true) {
                try {
                    var msg = msgQueue.Take(cts.Token);
                    msg.isQueued.Set(false);
                    var jsonStr = RabiJson.Stringify(msg, playerId);
                    await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonStr)), WebSocketMessageType.Text, true, cts.Token);
                } catch (OperationCanceledException) {
                    break;
                }
            }
        }

        protected bool Requeue(int id) {
            if (msgLookup.TryGetValue(id, out var msg)) {
                return msg.TryQueue(msgQueue);
            }
            return false;
        }

        public void Queue(object msg) {
            msgQueue.Add(new Message {
                id = lastMsgId.Next,
                message = msg,
            });
        }

        public bool Connect(WebSocket ws) {
            this.ws = ws;
            return true;
        }
        #endregion

        #region Receive Msg
        protected async Task ReceiveMsgLoop() {
            var byteArr = new ArraySegment<byte>(new byte[1024]);
            while (true) {
                try {
                    var msg = await ws.ReceiveAsync(byteArr, cts.Token);
                    if (!msg.EndOfMessage) {
                        // Too long, refuse to parse
                        while (!(await ws.ReceiveAsync(byteArr, cts.Token)).EndOfMessage) { }
                        break;
                    }
                    if (msg.MessageType != WebSocketMessageType.Text) {
                        continue;
                    }
                } catch (OperationCanceledException) {
                    break;
                }
            }
        }
        #endregion

        #region IDisposable
        protected void Dispose(bool disposing) {
            if (disposing) {
                cts.Cancel();
                cts.Dispose();
                ws = null;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}