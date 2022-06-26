using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Rpc {
    public class UserServiceImpl : UserService.UserServiceBase {
        private readonly ILogger<UserServiceImpl> logger;
        private readonly UserList userList;

        public UserServiceImpl(ILogger<UserServiceImpl> logger, UserList userList) {
            this.logger = logger;
            this.userList = userList;
        }

        public override Task<UserInfoResponse> GetMyInfo(Empty request, ServerCallContext context) {
            if (context.UserState.TryGetValue("USER", out object obj) && obj is User user) {
                return Task.FromResult(new UserInfoResponse {
                    Id = user.id,
                    Nickname = user.nickname,
                    Room = user.room?.id ?? 0,
                    Status = user.status,
                });
            } else {
                throw new RpcException(new Status(StatusCode.Unavailable, "Cannot get info"));
            }
        }
    }
}
