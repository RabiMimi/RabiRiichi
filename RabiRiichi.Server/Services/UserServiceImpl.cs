using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Services {
    public class UserServiceImpl : UserService.UserServiceBase {
        private readonly ILogger<UserServiceImpl> logger;
        private readonly UserList userList;
        private readonly TokenService tokenService;

        public UserServiceImpl(ILogger<UserServiceImpl> logger, UserList userList, TokenService tokenService) {
            this.logger = logger;
            this.userList = userList;
            this.tokenService = tokenService;
        }

        public override Task<CreateUserResponse> CreateUser(CreateUserRequest request, ServerCallContext context) {
            var user = new User {
                nickname = request.Nickname
            };
            if (!userList.Add(user)) {
                throw new RpcException(new Status(StatusCode.Unavailable, "Cannot add user"));
            }
            return Task.FromResult(new CreateUserResponse {
                Id = user.id,
                AccessToken = tokenService.BuildToken(user.id)
            });
        }

        [Authorize]
        public override Task<UserInfoResponse> GetMyInfo(Empty request, ServerCallContext context) {
            var user = userList.Fetch(context);
            return Task.FromResult(new UserInfoResponse {
                Id = user.id,
                Nickname = user.nickname,
                Room = user.room?.id ?? 0,
                Status = user.status,
            });
        }
    }
}
