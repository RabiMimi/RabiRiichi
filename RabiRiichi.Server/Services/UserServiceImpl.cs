using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Services {
    public class UserServiceImpl : UserService.UserServiceBase {
        private readonly ILogger<UserServiceImpl> logger;
        private readonly RoomTaskQueue taskQueue;
        private readonly TokenService tokenService;

        public UserServiceImpl(ILogger<UserServiceImpl> logger, RoomTaskQueue taskQueue, TokenService tokenService) {
            this.logger = logger;
            this.taskQueue = taskQueue;
            this.tokenService = tokenService;
        }

        public CreateUserResponse CreateUser(CreateUserRequest request, UserList userList) {
            var user = new User {
                nickname = request.Nickname
            };
            if (!userList.Add(user)) {
                throw new RpcException(new Status(StatusCode.Internal, "Cannot add user"));
            }
            user.AddRoomListeners(taskQueue);
            return new CreateUserResponse {
                Id = user.id,
                AccessToken = tokenService.BuildToken(user.id)
            };
        }

        public override Task<CreateUserResponse> CreateUser(CreateUserRequest request, ServerCallContext context) {
            return taskQueue.Execute(queue => {
                return CreateUser(request, queue.userList);
            });
        }

        public UserInfoResponse GetMyInfo(User user) {
            return new UserInfoResponse {
                Id = user.id,
                Nickname = user.nickname,
                Room = user.room?.GetServerRoomStateMsg(),
                Status = user.status,
            };
        }

        [Authorize]
        public override Task<UserInfoResponse> GetMyInfo(Empty request, ServerCallContext context) {
            return taskQueue.Execute(queue => {
                var user = queue.userList.Fetch(context);
                return GetMyInfo(user);
            });
        }
    }
}
