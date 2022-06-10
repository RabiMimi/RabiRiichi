using RabiRiichi.Communication.Json;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace RabiRiichi.Server.Core {
    public class RabiWSContext {
        /// <summary>
        /// Websocket instance
        /// </summary>
        public readonly WebSocket ws;

        /// <summary>
        /// Player ID for this websocket
        /// </summary>
        public readonly int playerId;

        /// <summary>
        /// Message queue
        /// </summary>
        private readonly BlockingCollection<Connection.Message> msgQueue = new();

        /// <summary>
        /// Cancellation token source for all event loops related to this websocket.
        /// </summary>
        public readonly CancellationTokenSource cts = new();

        /// <summary>
        /// Callback when a message is received.
        /// </summary>
        public Action<JsonElement> OnReceive;

        /// <summary>
        /// Callback when the player is disconnected.
        /// </summary>
        public System.Action OnDisconnect;

        public RabiWSContext(WebSocket ws, int playerId) {
            this.ws = ws;
            this.playerId = playerId;
        }

        /// <summary>
        /// Run all loops related to this websocket.<br/>
        /// Must call this method before using any callbacks.
        /// </summary>
        public async Task RunLoops(params Task[] extraTasks) {
            Task.WaitAll(
                extraTasks.Append(SendMsgLoop()).Append(ReceiveMsgLoop()).ToArray()
            );
            // Close connection.
            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            // Invoke callback. This will be called once and only once.
            OnDisconnect?.Invoke();
            // Dispose cancellation token source.
            cts.Dispose();
        }

        /// <summary>
        /// Queue a message to be sent.
        /// </summary>
        internal void Queue(Connection.Message msg) {
            msg.TryQueue(msgQueue);
        }

        /// <summary>
        /// Notify that no more message will be sent.
        /// </summary>
        internal void Close() {
            msgQueue.CompleteAdding();
        }

        private async Task SendMsgLoop() {
            while (true) {
                try {
                    var msg = msgQueue.Take(cts.Token);
                    msg.isQueued.Set(false);
                    var jsonStr = RabiJson.Stringify(msg, playerId);
                    await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonStr)),
                        WebSocketMessageType.Text, true, cts.Token);
                } catch (OperationCanceledException) {
                    // Cancellation token cancelled. Could be:
                    // 1. A new ws connection coming in
                    // 2. Client informed that they closed the connection
                    return;
                } catch (InvalidOperationException) {
                    // Cannot take from queue anymore.
                    // The upper layer has specified that no more message is being sent,
                    // so we can safely close the connection. No further connection will be made.
                    break;
                }
            }
            // Notify other loops to stop
            cts.Cancel();
        }

        private async Task ReceiveMsgLoop() {
            var byteArr = new ArraySegment<byte>(new byte[4 * 1024]);
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
                        OnReceive?.Invoke(json);
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
            // Notify other loops to stop
            cts.Cancel();
        }
    }
}