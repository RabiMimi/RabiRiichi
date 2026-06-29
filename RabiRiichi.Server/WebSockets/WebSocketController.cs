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
      UserServiceImpl userService) : ControllerBase {
    private readonly ILogger<WebSocketController> logger = logger;
    private readonly RoomTaskQueue taskQueue = taskQueue;
    private readonly TokenService tokenService = tokenService;
    private readonly InfoServiceImpl infoService = infoService;
    private readonly RoomServiceImpl roomService = roomService;
    private readonly UserServiceImpl userService = userService;

    private Task HandlePublic(Connection connection, ClientMessageDto msg) {
      return taskQueue.Execute(queue => {
        try {
          if (msg.ClientRequest?.GetInfo != null) {
            connection.Queue(ProtoUtils.CreateDto(infoService.GetInfo(), msg.Id));
          } else if (msg.ClientRequest?.CreateUser != null) {
            var req = msg.ClientRequest.CreateUser;
            var res = userService.CreateUser(req, queue.userList);
            connection.Queue(ProtoUtils.CreateDto(res, msg.Id));
          }
        } catch (RpcException e) {
          connection.Queue(ProtoUtils.CreateDto(new ServerErrorResponse {
            Status = e.Status.ToString(),
            Message = e.Message,
          }, msg.Id));
        }
      });
    }

    private Task HandlePrivate(User user, Connection connection, ClientMessageDto msg) {
      return taskQueue.Execute(queue => {
        try {
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
          } else if (msg.ClientRequest?.GetMyInfo != null) {
            var req = msg.ClientRequest.GetMyInfo;
            var res = userService.GetMyInfo(user);
            connection.Queue(ProtoUtils.CreateDto(res, msg.Id));
          }
        } catch (RpcException e) {
          connection.Queue(ProtoUtils.CreateDto(new ServerErrorResponse {
            Status = e.Status.StatusCode.ToString(),
            Message = e.Status.Detail,
          }, msg.Id));
        }
      });
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

      if (!tokenService.IsTokenValid(signIn.AccessToken, out var uid)) {
        return await FailSignIn();
      }

      var user = await taskQueue.Execute(queue => {
        return queue.userList.Get(uid);
      });
      if (user == null) {
        return await FailSignIn();
      }
      await adapter.WriteAsync(ProtoUtils.CreateDto(
          userService.GetMyInfo(user), current.Id));
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
