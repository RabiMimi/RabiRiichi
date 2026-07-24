using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Services;
using RabiRiichi.Server.WebSockets;
using RabiRiichi.Utils;

namespace RabiRiichi.Arena.WebSockets {
  /// <summary>
  /// The public replay WebSocket handler for Arena's <c>/ws/public</c> endpoint
  /// (ARENA_DESIGN.md §12d). It mirrors the player server's
  /// <c>WebSocketController.ConnectPublic</c> structure so the EXISTING web
  /// client renders Arena replays unchanged: open the socket, run the shared
  /// <see cref="ConnectionExtensions.VersionHandShake(WebSocketAdapter, TimeSpan)"/>
  /// version-check first, then serve <c>GetReplay</c> (required) and
  /// <c>GetInfo</c> (drop-in). Auth, rooms, and gameplay are not needed here.
  ///
  /// The message-handling logic lives in the pure, socket-free
  /// <see cref="HandlePublicMessage"/> so it is unit-testable (bytes/decoded
  /// message in → response out) without spinning up Kestrel or a real socket.
  /// This composes the already-public server building blocks
  /// (<see cref="WebSocketAdapter"/>, <see cref="ReplayStore"/>,
  /// <see cref="InfoServiceImpl"/>, <see cref="ProtoUtils"/>) rather than copying
  /// them.
  /// </summary>
  public sealed class ArenaPublicWebSocket {
    private readonly ReplayStore replayStore;
    private readonly InfoServiceImpl infoService;

    public ArenaPublicWebSocket(ReplayStore replayStore, InfoServiceImpl infoService) {
      this.replayStore = replayStore
          ?? throw new ArgumentNullException(nameof(replayStore));
      this.infoService = infoService
          ?? throw new ArgumentNullException(nameof(infoService));
    }

    /// <summary>
    /// Handles one already-accepted public WebSocket to completion: wraps it in
    /// the server's <see cref="WebSocketAdapter"/>, runs the version handshake,
    /// then loops reading client messages and writing back responses until the
    /// socket closes or <paramref name="requestAborted"/> fires. Mirrors
    /// <c>ConnectPublic</c>.
    /// </summary>
    public async Task HandleConnectionAsync(
        WebSocketAdapter adapter, CancellationToken requestAborted) {
      // Version-gate every connection before serving any request (§12d).
      if (!await adapter.VersionHandShake(TimeSpan.FromSeconds(15))) {
        await adapter.Close();
        return;
      }

      try {
        while (!requestAborted.IsCancellationRequested
            && await adapter.MoveNext(requestAborted)) {
          var reply = HandlePublicMessage(adapter.Current);
          if (reply != null) {
            await adapter.WriteAsync(reply, requestAborted);
          }
        }
      } catch (OperationCanceledException) {
        // Client disconnected or host is shutting down.
      } finally {
        await adapter.Close();
      }
    }

    /// <summary>
    /// Pure request→response handler for the two public requests Arena serves:
    /// <c>GetReplay</c> and <c>GetInfo</c>. Returns the response DTO, or null for
    /// messages that need no reply (e.g. a version-check echo). Errors are mapped
    /// to a <c>ServerErrorResponse</c> exactly like the server's
    /// <c>HandlePublic</c>. No sockets, no DI scope — trivially unit-testable.
    /// </summary>
    public ServerMessageDto HandlePublicMessage(ClientMessageDto msg) {
      if (msg == null) {
        return null;
      }
      try {
        if (msg.ClientRequest?.GetInfo != null) {
          return ProtoUtils.CreateDto(infoService.GetInfo(), msg.Id);
        }
        if (msg.ClientRequest?.GetReplay != null) {
          var gameId = msg.ClientRequest.GetReplay.GameId;
          if (!ReplayStore.IsValidGameId(gameId)) {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid game ID"));
          }
          var replay = replayStore.GetReplay(gameId);
          if (replay == null) {
            throw new RpcException(new Status(StatusCode.NotFound, "Replay not found"));
          }
          return ProtoUtils.CreateDto(replay, msg.Id);
        }
      } catch (RpcException e) {
        return ProtoUtils.CreateDto(new ServerErrorResponse {
          Status = e.Status.StatusCode.ToString(),
          Message = e.Status.Detail,
        }, msg.Id);
      }
      // Unknown / unsupported public request: no reply (matches server behavior).
      return null;
    }
  }
}
