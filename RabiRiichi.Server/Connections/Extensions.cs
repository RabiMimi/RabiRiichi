using RabiRiichi.Generated.Events;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Utils;

namespace RabiRiichi.Server.Connections {
  public static class ConnectionExtensions {
    #region DTO
    public static ServerMessageDto CreateDto(this ServerMsg serverMsg, int respondTo = 0) {
      return new ServerMessageDto {
        RespondTo = respondTo,
        ServerMsg = serverMsg
      };
    }

    public static ServerMessageDto CreateDto(this EventMsg ev, int respondTo = 0) {
      return new ServerMessageDto {
        RespondTo = respondTo,
        Event = ev
      };
    }

    public static ServerMessageDto CreateDto(this ServerResponse resp, int respondTo = 0) {
      return new ServerMessageDto {
        RespondTo = respondTo,
        ServerResp = resp,
      };
    }
    #endregion

    public static async Task<ClientMessageDto> WaitResponse(this ServerMessageWrapper msg,
        TimeSpan? timeout = null) {
      var task = msg.responseTcs.Task;
      if (timeout == null) {
        timeout = ServerConstants.RESPONSE_TIMEOUT;
      }
      try {
        return await task.WaitAsync(timeout.Value);
      } catch (TimeoutException) {
        return null;
      }
    }

    /// <summary>
    /// The single version-handshake routine shared by every connection: builds
    /// the server version-check message, hands it to a transport-specific
    /// <paramref name="exchange"/> that sends it and returns the client's reply
    /// (or null on timeout/disconnect/no-reply), and validates that reply.
    /// Returns false (caller should close) on any incompatible/missing reply.
    /// </summary>
    private static async Task<bool> VersionHandShake(
        Func<ServerMessageDto, Task<ClientVersionCheckMsg>> exchange) {
      var request = ProtoUtils.CreateDto(new ServerVersionCheckMsg {
        Game = ServerConstants.GAME,
        GameVersion = RabiRiichi.VERSION,
        Server = ServerConstants.SERVER,
        ServerVersion = ServerConstants.SERVER_VERSION,
        MinClientVersion = ServerConstants.MIN_CLIENT_VERSION,
      });
      var reply = await exchange(request);
      if (reply == null) {
        return false;
      }
      if (!ServerUtils.IsClientVersionSupported(reply.ClientVersion)) {
        return false;
      }
      return ServerUtils.IsServerVersionSupported(reply.MinServerVersion);
    }

    /// <summary>
    /// Version handshake over the authenticated streaming connection, using its
    /// message-queue + response correlation.
    /// </summary>
    public static Task<bool> VersionHandShake(this RabiStreamingContext ctx) {
      return VersionHandShake(async request => {
        var inMsg = await ctx.connection.Queue(request).WaitResponse();
        return inMsg?.ClientMsg?.VersionCheckMsg;
      });
    }

    /// <summary>
    /// Version handshake over the raw (pre-streaming) public socket, used before
    /// any public request (e.g. replay viewing) is served, so it is version-gated
    /// too. Reads until the version-check reply arrives (ignoring anything the
    /// client pipelined ahead of it) or the deadline hits.
    /// </summary>
    public static Task<bool> VersionHandShake(
        this WebSockets.WebSocketAdapter adapter, TimeSpan timeout) {
      return VersionHandShake(async request => {
        using var cts = new CancellationTokenSource(timeout);
        try {
          await adapter.WriteAsync(request, cts.Token);
          while (await adapter.MoveNext(cts.Token)) {
            var reply = adapter.Current?.ClientMsg?.VersionCheckMsg;
            if (reply != null) {
              return reply;
            }
          }
          return null;
        } catch (OperationCanceledException) {
          return null;
        }
      });
    }

    public static void AddRoomListeners(this User user, RoomTaskQueue taskQueue) {
      user.connection.OnReceive += dto => {
        var msg = dto.ClientMsg?.RoomUpdateMsg;
        if (msg != null) {
          _ = taskQueue.Execute(() => {
            switch (msg.Status) {
              case UserStatus.Ready:
                user.room?.GetReady(user);
                break;
              case UserStatus.InRoom:
                user.room?.CancelReady(user);
                break;
              case UserStatus.None:
                user.room?.RemovePlayer(user);
                break;
              default:
                break;
            }
          });
        }

        var chatMsg = dto.ClientMsg?.ChatMsg;
        if (chatMsg != null) {
          _ = taskQueue.Execute(() => {
            user.room?.BroadcastChatMessage(user, chatMsg);
          });
        }
      };

      user.connection.OnDisconnectContext += rabiCtx => {
        _ = Task.Run(async () => {
          await Task.Delay(ServerConstants.RECONNECT_GRACE_PERIOD);
          _ = taskQueue.Execute(() => {
            if (user.connection.Current == rabiCtx) {
              user.room?.RemovePlayer(user);
            }
          });
        });
      };
    }
  }
}
