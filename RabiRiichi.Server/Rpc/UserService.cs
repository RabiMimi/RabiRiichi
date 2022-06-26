using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Utils;

namespace RabiRiichi.Server.Rpc {
    public class UserServiceImpl : UserService.UserServiceBase {
        private readonly ILogger<UserServiceImpl> _logger;
        private readonly UserList userList;

        public UserServiceImpl(ILogger<UserServiceImpl> logger, UserList userList) {
            _logger = logger;
            this.userList = userList;
        }

        public override Task<UserInfoResponse> CreateUser(CreateUserRequest request, ServerCallContext context) {
            var user = new User {
                nickname = request.Nickname
            };
            if (!userList.Add(user)) {
                throw new RpcException(new Status(StatusCode.Unavailable, "Cannot add user"));
            }
            return Task.FromResult(new UserInfoResponse {
                Id = user.id,
                Nickname = user.nickname,
                Status = user.status,
                SessionCode = user.sessionCode.ToString("x")
            });
        }
    }
}
