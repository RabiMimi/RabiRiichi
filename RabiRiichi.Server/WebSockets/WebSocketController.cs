using Google.Protobuf.WellKnownTypes;
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
    public class WebSocketController : ControllerBase {
        private readonly ILogger<WebSocketController> logger;
        private readonly UserList userList;
        private readonly TokenService tokenService;
        private readonly InfoServiceImpl infoService;
        private readonly RoomServiceImpl roomService;
        private readonly UserServiceImpl userService;

        public WebSocketController(
            ILogger<WebSocketController> logger,
            UserList userList,
            TokenService tokenService,
            InfoServiceImpl infoService,
            RoomServiceImpl roomService,
            UserServiceImpl userService) {
            this.logger = logger;
            this.userList = userList;
            this.tokenService = tokenService;
            this.infoService = infoService;
            this.roomService = roomService;
            this.userService = userService;
        }

        private async Task HandlePublic(Connection connection, ClientMessageDto msg) {
            try {
                if (msg.ClientRequest?.GetInfo != null) {
                    connection.Queue(ProtoUtils.CreateDto(infoService.GetInfo(), msg.Id));
                } else if (msg.ClientRequest?.CreateUser != null) {
                    var req = msg.ClientRequest.CreateUser;
                    var res = await userService.CreateUser(req);
                    connection.Queue(ProtoUtils.CreateDto(res, msg.Id));
                }
            } catch (RpcException e) {
                connection.Queue(ProtoUtils.CreateDto(new ServerErrorResponse {
                    Status = e.Status.ToString(),
                    Message = e.Message,
                }, msg.Id));
            }
        }

        private async Task HandlePrivate(User user, Connection connection, ClientMessageDto msg) {
            try {
                if (msg.ClientRequest?.CreateRoom != null) {
                    connection.Queue(ProtoUtils.CreateDto(roomService.CreateRoom(user), msg.Id));
                } else if (msg.ClientRequest?.CreateUser != null) {
                    var req = msg.ClientRequest.CreateUser;
                    var res = await userService.CreateUser(req);
                    connection.Queue(ProtoUtils.CreateDto(res, msg.Id));
                } else if (msg.ClientRequest?.JoinRoom != null) {
                    var req = msg.ClientRequest.JoinRoom;
                    var res = await roomService.JoinRoom(req, user);
                    connection.Queue(new ServerResponse {
                        JoinRoom = res
                    }.CreateDto(msg.Id));
                }
            } catch (RpcException e) {
                connection.Queue(ProtoUtils.CreateDto(new ServerErrorResponse {
                    Status = e.Status.ToString(),
                    Message = e.Message,
                }, msg.Id));
            }
        }

        private async Task<User> HandleSignIn(WebSocketAdapter adapter) {
            if (!await adapter.MoveNext()) {
                return null;
            }

            var signIn = adapter.Current.ClientRequest?.SignIn;
            if (signIn == null) {
                return null;
            }

            if (!tokenService.IsTokenValid(signIn.AccessToken, out var uid)) {
                return null;
            }

            if (!userList.TryGet(uid, out var user)) {
                return null;
            }
            await adapter.WriteAsync(ProtoUtils.CreateDto(new ServerResponse {
                SignIn = new Empty()
            }));
            return user;
        }

        [HttpGet("public")]
        public async Task ConnectPublic() {
            if (HttpContext.WebSockets.IsWebSocketRequest) {
                using var webSocket =
                    await HttpContext.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext {
                        DangerousEnableCompression = true
                    });
                var adapter = new WebSocketAdapter(webSocket);
                var connection = new Connection();
                connection.Connect(adapter, adapter);
                connection.OnReceive += msg => _ = HandlePublic(connection, msg);
                try {
                    await Task.Delay(TimeSpan.FromDays(7), connection.Current.cts.Token);
                } catch (OperationCanceledException) { }
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
                    await adapter.WriteAsync(ProtoUtils.CreateDto(new ServerErrorResponse {
                        Status = Grpc.Core.StatusCode.Unauthenticated.ToString(),
                        Message = "Invalid access token"
                    }));
                    return;
                }

                var rabiCtx = user.Connect(adapter, adapter);
                if (rabiCtx == null) {
                    await adapter.WriteAsync(ProtoUtils.CreateDto(new ServerErrorResponse {
                        Status = Grpc.Core.StatusCode.Unauthenticated.ToString(),
                        Message = "Cannot find user"
                    }));
                    return;
                }
                if (!await rabiCtx.HandShake()) {
                    await adapter.WriteAsync(ProtoUtils.CreateDto(new ServerErrorResponse {
                        Status = Grpc.Core.StatusCode.FailedPrecondition.ToString(),
                        Message = "Handshake failed"
                    }));
                    return;
                }
                var connection = rabiCtx.connection;

                // Add handlers for Server events that are supposed to be handled by gRPC
                connection.OnReceive += msg => _ = HandlePublic(connection, msg);
                connection.OnReceive += msg => _ = HandlePrivate(user, connection, msg);

                var room = user.room;
                if (room != null) {
                    room.BroadcastRoomState();
                }
                try {
                    await Task.Delay(TimeSpan.FromDays(7), rabiCtx.cts.Token);
                } catch (OperationCanceledException) { }
            } else {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
    }
}