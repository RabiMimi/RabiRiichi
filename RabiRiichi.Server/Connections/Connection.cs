using Grpc.Core;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Utils;
using RabiRiichi.Utils;
using System.Collections.Concurrent;

namespace RabiRiichi.Server.Connections {
    public class Connection : IDisposable {
        /// <summary>
        /// Current streaming context. Null if not connected.<br/>
        /// Will not be reset to null when the connection is closed.
        /// </summary>
        protected RabiStreamingContext currentCtx;
        public RabiStreamingContext Current => currentCtx;

        /// <summary>
        /// Callback when a message is received.
        /// </summary>
        public Action<ClientMessageDto> OnReceive;

        /// <summary>
        /// Switch to a new context. Old connection will be closed.
        /// </summary>
        protected void SwitchContext(RabiStreamingContext newCts) {
            Interlocked.Exchange(ref currentCtx, newCts)?.Close();
        }

        /// <summary>
        /// Last message ID. Shared by all connections with the same player.
        /// </summary>
        public AutoIncrementInt msgId = new();

        /// <summary>
        /// Mapping from message ID to message. Shared by all connections with the same player.
        /// </summary>
        internal readonly ConcurrentDictionary<int, ServerMessageWrapper> serverMsgs = new();

        /// <summary>
        /// Create an outgoing message but do not send it.
        /// </summary>
        public ServerMessageWrapper CreateMessage(ServerMessageDto msg) {
            var ret = new ServerMessageWrapper(msg);
            ret.msg.Id = msgId.Next;
            return ret;
        }

        /// <summary>
        /// Add a message to queue. It will not be sent immediately.
        /// </summary>
        /// <param name="msg">Message to send</param>
        public void Queue(ServerMessageWrapper msg) {
            serverMsgs.TryAdd(msg.msg.Id, msg);
            if (currentCtx == null) {
                throw new InvalidOperationException("Websocket is not connected");
            }
            currentCtx.Queue(msg);
        }

        /// <summary>
        /// Add a message to queue. It will not be sent immediately.
        /// </summary>
        public ServerMessageWrapper Queue(ServerMessageDto msg) {
            var ret = CreateMessage(msg);
            Queue(ret);
            return ret;
        }

        /// <summary>
        /// Connects to a stream. Existing connection will be closed.
        /// </summary>
        public RabiStreamingContext Connect(
            IAsyncStreamReader<ClientMessageDto> requestStream,
            IServerStreamWriter<ServerMessageDto> responseStream) {
            var ctx = new RabiStreamingContext(this, requestStream, responseStream);
            ctx.OnReceive += incoming => OnReceive?.Invoke(incoming);

            // Cancel previous connection and switch context
            SwitchContext(ctx);

            // Start message loops before context switch
            _ = ctx.RunLoops(HeartBeatRecvLoop(ctx));

            return ctx;
        }

        /// <summary>
        /// When a heartbeat arrives, reply.
        /// </summary>
        private void OnReceiveHeartBeat(RabiStreamingContext ctx, TwoWayHeartBeatMsg msg, int respondTo) {
            // Respond to the heartbeat
            var response = new TwoWayHeartBeatMsg {
                MaxId = msgId,
            };
            response.RequestingIds.AddRange(ctx.GetMissingMsgIds().Take(16));
            ctx.Queue(new ServerMessageWrapper(
                new ServerMessageDto {
                    Id = -1,
                    RespondTo = respondTo,
                    ServerMsg = ProtoUtils.CreateServerMsg(response),
                }
            ));
            // Client requesting events to be resent
            foreach (var evt in msg.RequestingIds) {
                if (serverMsgs.TryGetValue(evt, out var oldMsg)) {
                    ctx.Queue(oldMsg);
                }
            }
        }

        /// <summary>
        /// Receives a heartbeat package.
        /// If no response is received in 15 seconds, the connection will be closed.
        /// </summary>
        private async Task HeartBeatRecvLoop(RabiStreamingContext ctx) {
            TaskCompletionSource received = null;
            ctx.OnReceive += (ClientMessageDto incoming) => {
                // Always resets heartbeat timer even if the message is not a heartbeat
                received?.TrySetResult();
                // Check if the message is responding to a server message
                if (serverMsgs.TryGetValue(incoming.RespondTo, out var msg)) {
                    msg.responseTcs.TrySetResult(incoming);
                }
                var heartBeat = incoming.ClientMsg?.HeartBeatMsg;
                if (heartBeat != null) {
                    OnReceiveHeartBeat(ctx, heartBeat, incoming.Id);
                }
            };
            while (true) {
                received = new();
                try {
                    var delayTask = Task.Delay(ServerConstants.RESPONSE_TIMEOUT, ctx.cts.Token);
                    var completed = await Task.WhenAny(received.Task, delayTask);
                    if (completed == received.Task) {
                        // Received a message from client
                        continue;
                    } else {
                        // No response from client, close connection
                        ctx?.Close();
                        break;
                    }
                } catch (OperationCanceledException) {
                    // Closed connection
                    break;
                }
            }
        }

        /// <summary>
        /// Closes the connection. <br/>No more web sockets or messages are accepted,
        /// and the queued messages will be sent before closing.
        /// </summary>
        public void Close() {
            currentCtx?.Close();
        }

        protected void Dispose(bool disposing) {
            if (disposing) {
                SwitchContext(null);
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