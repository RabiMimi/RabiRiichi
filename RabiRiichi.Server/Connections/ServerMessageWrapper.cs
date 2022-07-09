using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Utils;

namespace RabiRiichi.Server.Connections {
    /// <summary>
    /// Wrapper for messages to send to client.
    /// </summary>
    public class ServerMessageWrapper {
        public readonly ServerMessageDto msg;
        public readonly AtomicBool isQueued = new();
        public readonly TaskCompletionSource<ClientMessageDto> responseTcs = new();

        public ServerMessageWrapper(ServerMessageDto msg) {
            this.msg = msg;
        }
    }
}