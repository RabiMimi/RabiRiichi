using RabiRiichi.Communication.Json;
using RabiRiichi.Server.Messages;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace RabiRiichi.Server.Utils {
    public class RabiWSContext {
        /// <summary>
        /// Parent connection.
        /// </summary>
        public readonly Connection connection;

        /// <summary>
        /// Websocket instance
        /// </summary>
        public readonly WebSocket ws;

        /// <summary>
        /// Whether the connection is closing and no more messages can be queued.
        /// </summary>
        public bool IsClosing { get; private set; } = false;

        /// <summary>
        /// Player ID for this websocket
        /// </summary>
        public readonly int playerId;

        /// <summary>
        /// Message queue
        /// </summary>
        private readonly ConcurrentBag<OutMessage> msgQueue = new();

        /// <summary>
        /// Cancellation token source for all event loops related to this websocket.
        /// </summary>
        public readonly CancellationTokenSource cts = new();

        /// <summary>
        /// Maximum client message ID.
        /// </summary>
        public int maxClientMsgId = 0;

        /// <summary>
        /// Maximum received message ID.
        /// </summary>
        public int maxReceivedMsgId = 0;

        /// <summary>
        /// Callback when a message is received.
        /// </summary>
        public Action<InMessage> OnReceive;

        /// <summary>
        /// Callback when the player is disconnected.
        /// </summary>
        public Action OnDisconnect;

        public RabiWSContext(Connection connection, WebSocket ws, int playerId) {
            this.connection = connection;
            this.ws = ws;
            this.playerId = playerId;
        }

        /// <summary>
        /// Run all loops related to this websocket.<br/>
        /// Must call this method before using any callbacks.
        /// </summary>
        public async Task RunLoops(params Task[] extraTasks) {
            await Task.WhenAll(
                extraTasks
                    .Append(SendMsgLoop())
                    .Append(ReceiveMsgLoop())
                    .ToArray()
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
        internal void Queue(OutMessage msg) {
            if (IsClosing || msg.isQueued.Exchange(true)) {
                return;
            }
            msgQueue.Add(msg);
        }

        /// <summary>
        /// Notify that no more message will be sent.
        /// </summary>
        internal void Close() {
            IsClosing = true;
        }

        private async Task SendMsgLoop() {
            while (true) {
                try {
                    await Task.Delay(100, cts.Token);
                    while (msgQueue.TryTake(out var msg)) {
                        msg.isQueued.Set(false);
                        var jsonStr = RabiJson.Stringify(msg, playerId);
                        Console.WriteLine($"Sending: {jsonStr}");
                        await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonStr)),
                            WebSocketMessageType.Text, true, cts.Token);
                    }
                } catch (OperationCanceledException) {
                    // Cancellation token cancelled. Could be:
                    // 1. A new ws connection coming in
                    // 2. Client informed that they closed the connection
                    return;
                } catch (WebSocketException) {
                    // Connection closed.
                    return;
                }
            }
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
                        Console.WriteLine($"Received: {jsonStr}");
                        var inMsg = JsonSerializer.Deserialize<InMessage>(jsonStr, InMessage.jsonSerializerOptions);
                        if (inMsg != null) {
                            RabiInterlocked.ExchangeMax(ref maxReceivedMsgId, inMsg.id);
                            OnReceive?.Invoke(inMsg);
                        }
                    }
                } catch (OperationCanceledException) {
                    // Cancellation token cancelled. Could be:
                    // 1. A new ws connection coming in
                    // 2. Client informed that they closed the connection
                    return;
                } catch (JsonException) {
                    // Invalid message
                    continue;
                } catch (WebSocketException) {
                    // Connection closed.
                    break;
                }
            }
            // Notify other loops to stop
            cts.Cancel();
        }
    }
}