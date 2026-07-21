using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Services;

namespace RabiRiichi.Server.WebSockets {
  [ApiController]
  [Route("/ws")]
  public class WebSocketController(
      ILogger<WebSocketController> logger,
      RoomTaskQueue taskQueue,
      TokenService tokenService,
      InfoServiceImpl infoService,
      RoomServiceImpl roomService,
      UserServiceImpl userService,
      ReplayStore replayStore,
      DbService dbService) : ControllerBase {
    private readonly ILogger<WebSocketController> logger = logger;
    private readonly RoomTaskQueue taskQueue = taskQueue;
    private readonly TokenService tokenService = tokenService;
    private readonly InfoServiceImpl infoService = infoService;
    private readonly RoomServiceImpl roomService = roomService;
    private readonly UserServiceImpl userService = userService;
    private readonly ReplayStore replayStore = replayStore;
    private readonly DbService dbService = dbService;

    private Task HandlePublic(Connection connection, ClientMessageDto msg) {
      return taskQueue.Execute(queue => {
        try {
          if (msg.ClientRequest?.GetInfo != null) {
            connection.Queue(ProtoUtils.CreateDto(infoService.GetInfo(), msg.Id));
          } else if (msg.ClientRequest?.CreateUser != null) {
            var req = msg.ClientRequest.CreateUser;
            var res = userService.CreateUser(req, queue.userList);
            connection.Queue(ProtoUtils.CreateDto(res, msg.Id));
          } else if (msg.ClientRequest?.LoginUser != null) {
            var req = msg.ClientRequest.LoginUser;
            var res = userService.LoginUser(req, queue.userList);
            connection.Queue(ProtoUtils.CreateDto(res, msg.Id));
          } else if (msg.ClientRequest?.ChangePassword != null) {
            var req = msg.ClientRequest.ChangePassword;
            var res = userService.ChangePassword(req, queue.userList);
            connection.Queue(ProtoUtils.CreateDto(res, msg.Id));
          } else if (msg.ClientRequest?.GetReplay != null) {
            var req = msg.ClientRequest.GetReplay;
            if (!ReplayStore.IsValidGameId(req.GameId)) {
              throw new RpcException(new Status(Grpc.Core.StatusCode.InvalidArgument, "Invalid game ID"));
            }
            var replay = replayStore.GetReplay(req.GameId);
            if (replay == null) {
              throw new RpcException(new Status(Grpc.Core.StatusCode.NotFound, "Replay not found"));
            }
            connection.Queue(ProtoUtils.CreateDto(replay, msg.Id));
          }
        } catch (RpcException e) {
          connection.Queue(ProtoUtils.CreateDto(new ServerErrorResponse {
            Status = e.Status.ToString(),
            Message = e.Message,
          }, msg.Id));
        }
      });
    }

    private async Task HandlePrivate(User user, Connection connection, ClientMessageDto msg) {
      try {
        // AddAi is handled separately: LLM validation makes a network call that
        // must NOT run inside the serialized room task queue (it would stall all
        // rooms). AddAiAsync validates outside the queue and only queues the
        // fast room mutation.
        if (msg.ClientRequest?.AddAi != null) {
          var res = await roomService.AddAiAsync(msg.ClientRequest.AddAi, user);
          connection.Queue(ProtoUtils.CreateDto(res, msg.Id));
          return;
        }

        await taskQueue.Execute(queue => {
          if (msg.ClientRequest?.CreateRoom != null) {
            connection.Queue(ProtoUtils.CreateDto(roomService.CreateRoom(msg.ClientRequest.CreateRoom, queue.roomList, user), msg.Id));
          } else if (msg.ClientRequest?.CreateUser != null) {
            var req = msg.ClientRequest.CreateUser;
            var res = userService.CreateUser(req, queue.userList);
            connection.Queue(ProtoUtils.CreateDto(res, msg.Id));
          } else if (msg.ClientRequest?.JoinRoom != null) {
            var req = msg.ClientRequest.JoinRoom;
            var res = roomService.JoinRoom(req, queue.roomList, user);
            connection.Queue(ProtoUtils.CreateDto(res, msg.Id));
          } else if (msg.ClientRequest?.RemoveRoomPlayer != null) {
            var req = msg.ClientRequest.RemoveRoomPlayer;
            var res = roomService.RemoveRoomPlayer(req, queue.roomList, user);
            connection.Queue(ProtoUtils.CreateDto(res, msg.Id));
          } else if (msg.ClientRequest?.GetMyInfo != null) {
            var req = msg.ClientRequest.GetMyInfo;
            var res = userService.GetMyInfo(user);
            connection.Queue(ProtoUtils.CreateDto(res, msg.Id));
          } else if (msg.ClientRequest?.UpdateProfile != null) {
            var req = msg.ClientRequest.UpdateProfile;
            var res = userService.UpdateProfile(req, user);
            connection.Queue(ProtoUtils.CreateDto(res, msg.Id));
          }
        });
      } catch (RpcException e) {
        connection.Queue(ProtoUtils.CreateDto(new ServerErrorResponse {
          Status = e.Status.StatusCode.ToString(),
          Message = e.Status.Detail,
        }, msg.Id));
      }
    }

    private async Task<User> HandleSignIn(WebSocketAdapter adapter) {
      var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
      try {
        if (!await adapter.MoveNext(cts.Token)) {
          return null;
        }
      } catch (OperationCanceledException) {
        return null;
      }

      var current = adapter.Current;
      var signIn = current?.ClientRequest?.SignIn;

      async Task<User> FailSignIn() {
        await adapter.WriteAsync(ProtoUtils.CreateDto(new ServerErrorResponse {
          Status = Grpc.Core.StatusCode.Unauthenticated.ToString(),
          Message = "Authentication failed",
        }, current.Id));
        return null;
      }

      if (signIn == null) {
        return await FailSignIn();
      }

      if (!tokenService.IsTokenValid(signIn.AccessToken, out var uid, out var tokenVersion)) {
        return await FailSignIn();
      }

      // Validate the token version against the DB exactly once per connection.
      // A password change bumps the stored version, invalidating older tokens.
      // The DB read stays off the per-request hot path (only the JWT signature
      // is checked there).
      var dbUser = dbService.GetUserById(uid);
      if (dbUser == null || dbUser.TokenVersion != tokenVersion) {
        return await FailSignIn();
      }

      var user = await taskQueue.Execute(queue => {
        var u = queue.userList.Get(uid);
        if (u == null) {
          u = new User {
            id = dbUser.Id,
            nickname = dbUser.UserData.Nickname
          };
          u.AddRoomListeners(taskQueue);
          queue.userList.Add(u);
        }
        return u;
      });
      if (user == null) {
        return await FailSignIn();
      }
      var myInfo = userService.GetMyInfo(user);
      myInfo.AccessToken = tokenService.BuildToken(user.id, dbUser.TokenVersion);
      await adapter.WriteAsync(ProtoUtils.CreateDto(myInfo, current.Id));
      return user;
    }

    [HttpGet("public")]
    public async Task ConnectPublic() {
      if (HttpContext.WebSockets.IsWebSocketRequest) {
        using var webSocket =
            await HttpContext.WebSockets.AcceptWebSocketAsync();
        var adapter = new WebSocketAdapter(webSocket);
        var connection = new Connection();
        void PublicListener(ClientMessageDto msg) {
          _ = HandlePublic(connection, msg);
        }

        connection.OnReceive += PublicListener;
        try {
          connection.Connect(adapter, adapter);
          // Wait until the stream closes (its cts) OR the request is aborted.
          // RequestAborted fires on client disconnect AND on host shutdown
          // (Ctrl+C), so this no longer blocks graceful shutdown.
          using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
              connection.Current.cts.Token, HttpContext.RequestAborted);
          await Task.Delay(Timeout.InfiniteTimeSpan, linkedCts.Token);
        } catch (OperationCanceledException) { } finally {
          connection.Close();
          connection.OnReceive -= PublicListener;
        }
      } else {
        HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
      }
    }

    [HttpGet("connect")]
    public async Task ConnectLoggedIn() {
      if (HttpContext.WebSockets.IsWebSocketRequest) {
        using var webSocket =
            await HttpContext.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext {
              DangerousEnableCompression = true
            });
        var adapter = new WebSocketAdapter(webSocket);

        var user = await HandleSignIn(adapter);
        if (user == null) {
          await adapter.Close();
          return;
        }

        void PublicListener(ClientMessageDto msg) {
          _ = HandlePublic(user.connection, msg);
        }

        void UserListener(ClientMessageDto msg) {
          _ = HandlePrivate(user, user.connection, msg);
        }

        RabiStreamingContext rabiCtx = null;

        try {
          rabiCtx = await taskQueue.Execute(() => user.connection.Connect(adapter, adapter));

          if (rabiCtx == null) {
            await adapter.WriteAsync(ProtoUtils.CreateDto(new ServerErrorResponse {
              Status = Grpc.Core.StatusCode.Unauthenticated.ToString(),
              Message = "Cannot find user"
            }));
            await adapter.Close();
            return;
          }

          // Add handlers for Server events that are supposed to be handled by gRPC
          user.connection.OnReceive += PublicListener;
          user.connection.OnReceive += UserListener;

          if (!await rabiCtx.HandShake()) {
            rabiCtx.Close();
            return;
          }

          await taskQueue.Execute(() => {
            user.room?.BroadcastRoomState();
            user.room?.SyncGameTo(user);
          });
          // Wait until the stream closes (its cts) OR the request is aborted.
          // RequestAborted fires on client disconnect AND on host shutdown
          // (Ctrl+C), so this no longer blocks graceful shutdown.
          using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
              rabiCtx.cts.Token, HttpContext.RequestAborted);
          await Task.Delay(Timeout.InfiniteTimeSpan, linkedCts.Token);
        } catch (OperationCanceledException) { } finally {
          rabiCtx?.Close();
          user.connection.OnReceive -= PublicListener;
          user.connection.OnReceive -= UserListener;
        }
      } else {
        HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
      }
    }
  }
}
