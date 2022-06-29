using Grpc.Core;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.WebSockets;
using RabiRiichi.Util;
using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace RabiRiichi.Server.Connections {
    public class RabiStreamingContext {
        /// <summary>
        /// Parent connection.
        /// </summary>
        public readonly Connection connection;

        /// <summary>
        /// Cancellation token source for all event loops related to this stream.
        /// </summary>
        public readonly CancellationTokenSource cts = new();

        /// <summary>
        /// Callback when a message is received.
        /// </summary>
        public Action<ClientMessageDto> OnReceive;

        /// <summary>
        /// Callback when the player is disconnected.
        /// </summary>
        public Action OnDisconnect;

        /// <summary>
        /// Incoming message stream.
        /// </summary>
        private readonly IAsyncStreamReader<ClientMessageDto> requestStream;

        /// <summary>
        /// Outgoing message stream.
        /// </summary>
        private readonly IServerStreamWriter<ServerMessageDto> responseStream;

        /// <summary>
        /// Message queue
        /// </summary>
        private readonly ConcurrentBag<ServerMessageWrapper> msgQueue = new();

        /// <summary>
        /// Whether the connection is closing and no more messages can be queued.
        /// </summary>
        private readonly AtomicBool IsClosing = new(false);

        /// <summary>
        /// Mapping from message ID to message. Only used in this connection.
        /// </summary>
        private readonly ConcurrentDictionary<int, ClientMessageDto> clientMsgs = new();

        /// <summary>
        /// Maximum message ID sent by the client.
        /// </summary>
        private int maxClientMsgId = 0;

        /// <summary>
        /// Last broadcasted client message ID. Only used in this connection.
        /// </summary>
        private int lastBroadcastedMsgId = 0;

        public RabiStreamingContext(
            Connection connection,
            IAsyncStreamReader<ClientMessageDto> requestStream,
            IServerStreamWriter<ServerMessageDto> responseStream) {
            this.connection = connection;
            this.requestStream = requestStream;
            this.responseStream = responseStream;
        }

        /// <summary>
        /// Run all loops related to this connection.<br/>
        /// Must call this method before using any callbacks.
        /// </summary>
        public async Task RunLoops(params Task[] extraTasks) {
            await Task.WhenAll(
                extraTasks
                    .Append(SendMsgLoop())
                    .Append(ReceiveMsgLoop())
                    .ToArray()
            );
            // Close connection for websocket compatibility.
            if (requestStream is WebSocketAdapter adapter) {
                await adapter.Close();
            }
            // Invoke callback. This will be called once and only once.
            OnDisconnect?.Invoke();
            // Dispose cancellation token source.
            cts.Dispose();
        }

        /// <summary>
        /// Queue a message to be sent.
        /// </summary>
        internal void Queue(ServerMessageWrapper msg) {
            if (IsClosing || msg.isQueued.Exchange(true)) {
                return;
            }
            msgQueue.Add(msg);
        }

        /// <summary>
        /// Get missing message IDs so that we can request them from the client.
        /// </summary>
        internal IEnumerable<int> GetMissingMsgIds() {
            int maxId = maxClientMsgId;
            for (int i = lastBroadcastedMsgId; i <= maxId; i++) {
                if (!clientMsgs.ContainsKey(i)) {
                    yield return i;
                }
            }
        }

        /// <summary>
        /// Closes the connection and prevent any further messages from being sent.
        /// </summary>
        internal void Close() {
            if (IsClosing.Exchange(true)) {
                return;
            }
            cts.Cancel();
        }

        private async Task SendMsgLoop() {
            while (true) {
                try {
                    await Task.Delay(100, cts.Token);
                    while (msgQueue.TryTake(out var msg)) {
                        Console.WriteLine($"Sending: {msg.msg}");
                        msg.isQueued.Exchange(false);
                        await responseStream.WriteAsync(msg.msg, cts.Token);
                    }
                } catch (OperationCanceledException) {
                    // Cancellation token cancelled. Could be:
                    // 1. A new connection coming in
                    // 2. Client informed that they closed the connection
                    return;
                } catch (WebSocketException) {
                    // Using websocket adapter, client closed the connection.
                    return;
                }
            }
        }

        private async Task ReceiveMsgLoop() {
            try {
                while (await requestStream.MoveNext(cts.Token)) {
                    var msg = requestStream.Current;
                    if (msg == null) {
                        continue;
                    }
                    Console.WriteLine($"Received: {msg}");
                    maxClientMsgId = Math.Max(maxClientMsgId, msg.Id);

                    var heartBeat = msg.ClientMsg?.HeartBeatMsg;
                    if (heartBeat == null) {
                        // Regular message with positive ID.
                        clientMsgs.TryAdd(msg.Id, msg);
                        if (msg.Id == lastBroadcastedMsgId + 1) {
                            OnReceive?.Invoke(msg);
                            lastBroadcastedMsgId++;
                            // Broadcast other messages in order.
                            while (lastBroadcastedMsgId < maxClientMsgId
                                && clientMsgs.TryGetValue(lastBroadcastedMsgId + 1, out var clientMsg)) {
                                OnReceive?.Invoke(clientMsg);
                                lastBroadcastedMsgId++;
                            }
                        }
                    } else {
                        // Handle heart beat.
                        maxClientMsgId = Math.Max(maxClientMsgId, heartBeat.MaxId);
                        OnReceive?.Invoke(msg);
                    }
                }
            } catch (OperationCanceledException) {
                // Cancellation token cancelled. Could be:
                // 1. A new connection coming in
                // 2. Client informed that they closed the connection
                return;
            }
            // Notify other loops to stop
            cts.Cancel();
        }
    }
}