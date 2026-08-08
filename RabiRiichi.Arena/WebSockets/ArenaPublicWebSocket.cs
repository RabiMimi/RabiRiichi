using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Services;
using RabiRiichi.Server.Utils;
using RabiRiichi.Server.WebSockets;
using RabiRiichi.Utils;

namespace RabiRiichi.Arena.WebSockets {
  /// <summary>
  /// The public replay WebSocket handler for Arena's <c>/ws/public</c> endpoint
  /// (ARENA_DESIGN.md §12d). It mirrors the player server's
  /// <c>WebSocketController.ConnectPublic</c> structure so the EXISTING web
  /// client renders Arena replays unchanged: open the socket, run the
  /// version-check first, then serve <c>GetReplay</c> (required), <c>GetInfo</c>
  /// (drop-in) and heartbeats (so the client keeps the socket alive). Auth,
  /// rooms, and gameplay are not needed here.
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
    /// <summary>How long the client has to answer the version check.</summary>
    public static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Upper bound on requests remembered while the handshake is in flight, so a
    /// client that floods the socket before answering cannot grow this unbounded.
    /// </summary>
    private const int MaxPipelinedRequests = 8;

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
      var pipelined = new List<ClientMessageDto>();
      if (!await VersionHandShake(adapter, pipelined, requestAborted)) {
        await adapter.Close();
        return;
      }

      try {
        // Serve whatever the client sent while the handshake was still in
        // flight, in arrival order, before reading anything new.
        foreach (var early in pipelined) {
          var earlyReply = HandlePublicMessage(early);
          if (earlyReply != null) {
            await adapter.WriteAsync(earlyReply, requestAborted);
          }
        }
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
    /// Runs the version handshake, collecting into <paramref name="pipelined"/>
    /// every non-version-check message read along the way instead of dropping it.
    ///
    /// This deliberately does NOT reuse
    /// <see cref="ConnectionExtensions.VersionHandShake(WebSocketAdapter, TimeSpan)"/>:
    /// that helper discards anything the client pipelines ahead of its version
    /// reply, and the web client does exactly that — a second concurrent
    /// <c>fetchReplay</c> only awaits the socket's <c>open</c>, not the
    /// handshake, so its <c>GetReplay</c> lands first and was silently eaten.
    /// The request then never got a response and the replay page timed out.
    /// Version validation itself is still the server's
    /// (<see cref="ServerUtils"/>), so the gate stays identical.
    /// </summary>
    private static async Task<bool> VersionHandShake(
        WebSocketAdapter adapter,
        List<ClientMessageDto> pipelined,
        CancellationToken requestAborted) {
      var request = ProtoUtils.CreateDto(new ServerVersionCheckMsg {
        Game = ServerConstants.GAME,
        GameVersion = RabiRiichi.VERSION,
        Server = ServerConstants.SERVER,
        ServerVersion = ServerConstants.SERVER_VERSION,
        MinClientVersion = ServerConstants.MIN_CLIENT_VERSION,
      });
      using var timeoutCts = new CancellationTokenSource(HandshakeTimeout);
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
          timeoutCts.Token, requestAborted);
      try {
        await adapter.WriteAsync(request, linkedCts.Token);
        while (await adapter.MoveNext(linkedCts.Token)) {
          var current = adapter.Current;
          var reply = current?.ClientMsg?.VersionCheckMsg;
          if (reply == null) {
            if (current == null) {
              continue;
            }
            if (pipelined.Count < MaxPipelinedRequests) {
              pipelined.Add(current);
            } else {
              Logger.Warn("[Arena] Dropping request pipelined before the version "
                  + "handshake: buffer full.");
            }
            continue;
          }
          return ServerUtils.IsClientVersionSupported(reply.ClientVersion)
              && ServerUtils.IsServerVersionSupported(reply.MinServerVersion);
        }
        return false;
      } catch (OperationCanceledException) {
        return false;
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
      // The client pings every few seconds and closes the socket if a ping goes
      // unanswered, so an Arena replay socket used to die ~15s after connecting.
      // Mirror Connection.OnReceiveHeartBeat: id -1, respond to the ping, and
      // report maxId 0 — this handler never assigns outgoing message ids, so
      // there is nothing for the client to consider missing and re-request.
      if (msg.ClientMsg?.HeartBeatMsg != null) {
        return new ServerMessageDto {
          Id = -1,
          RespondTo = msg.Id,
          ServerMsg = ProtoUtils.CreateServerMsg(new TwoWayHeartBeatMsg { MaxId = 0 }),
        };
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
