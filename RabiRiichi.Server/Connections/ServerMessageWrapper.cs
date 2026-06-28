using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Utils;

namespace RabiRiichi.Server.Connections {
  /// <summary>
  /// Wrapper for messages to send to client.
  /// </summary>
  public class ServerMessageWrapper(ServerMessageDto msg) {
    public readonly ServerMessageDto msg = msg.Clone();
    public readonly AtomicBool isQueued = new();
    public readonly TaskCompletionSource<ClientMessageDto> responseTcs = new();
  }
}