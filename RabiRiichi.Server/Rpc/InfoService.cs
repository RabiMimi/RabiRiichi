using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Utils;

namespace RabiRiichi.Server.Rpc {
    public class PublicServiceImpl : PublicService.PublicServiceBase {
        private readonly ILogger<PublicServiceImpl> logger;
        private readonly UserList userList;
        private readonly TokenService tokenService;

        public PublicServiceImpl(ILogger<PublicServiceImpl> logger, UserList userList, TokenService tokenService) {
            this.logger = logger;
            this.userList = userList;
            this.tokenService = tokenService;
        }

        public override Task<GetInfoResponse> GetInfo(Empty request, ServerCallContext context) {
            return Task.FromResult(new GetInfoResponse {
                Game = ServerConstants.GAME,
                GameVersion = RabiRiichi.VERSION,
                Server = ServerConstants.SERVER,
                ServerVersion = ServerConstants.SERVER_VERSION,
                MinClientVersion = ServerConstants.MIN_CLIENT_VERSION,
            });
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
    }
}
