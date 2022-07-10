using Google.Protobuf;
using Grpc.Core;
using RabiRiichi.Server.Generated.Rpc;
using System.Net.WebSockets;

namespace RabiRiichi.Server.WebSockets {
    public class WebSocketAdapter : IAsyncStreamReader<ClientMessageDto>, IServerStreamWriter<ServerMessageDto> {
        private readonly WebSocket ws;
        private ClientMessageDto current;
        private readonly ArraySegment<byte> byteArr = new(new byte[4 * 1024]);

        public WebSocketAdapter(WebSocket ws) {
            this.ws = ws;
        }

        public ClientMessageDto Current => current;

        /// <summary>
        /// Not used
        /// </summary>
        public WriteOptions WriteOptions { get; set; }

        public async Task<bool> MoveNext(CancellationToken cancellationToken) {
            try {
                var msg = await ws.ReceiveAsync(byteArr, cancellationToken);
                if (!msg.EndOfMessage) {
                    // Too long, refuse to parse
                    while (!(await ws.ReceiveAsync(byteArr, cancellationToken)).EndOfMessage) { }
                    return await MoveNext(cancellationToken);
                }
                if (msg.MessageType == WebSocketMessageType.Close) {
                    // Client closes connection, cannot move next
                    return false;
                } else if (msg.MessageType == WebSocketMessageType.Binary) {
                    // Success
                    current = ClientMessageDto.Parser.ParseFrom(byteArr.Array, byteArr.Offset, msg.Count);
                    return true;
                } else {
                    // Invalid message type
                    return await MoveNext(cancellationToken);
                }
            } catch (WebSocketException) {
                // Connection closed
                return false;
            }
        }

        public Task WriteAsync(ServerMessageDto message) {
            return WriteAsync(message, CancellationToken.None);
        }

        public async Task WriteAsync(ServerMessageDto message, CancellationToken cancellationToken) {
            var bytes = message.ToByteArray();
            try {
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, cancellationToken);
            } catch (WebSocketException) {
                // Connection closed
            }
        }

        public async Task Close() {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
    }
}