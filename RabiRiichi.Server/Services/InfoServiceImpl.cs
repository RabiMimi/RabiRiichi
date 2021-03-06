using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Utils;

namespace RabiRiichi.Server.Services {
    public class InfoServiceImpl : InfoService.InfoServiceBase {
        private readonly ILogger<InfoServiceImpl> logger;

        public InfoServiceImpl(ILogger<InfoServiceImpl> logger) {
            this.logger = logger;
        }

        public GetInfoResponse GetInfo() {
            return new GetInfoResponse {
                Game = ServerConstants.GAME,
                GameVersion = RabiRiichi.VERSION,
                Server = ServerConstants.SERVER,
                ServerVersion = ServerConstants.SERVER_VERSION,
                MinClientVersion = ServerConstants.MIN_CLIENT_VERSION,
            };
        }

        public override Task<GetInfoResponse> GetInfo(Empty request, ServerCallContext context) {
            return Task.FromResult(GetInfo());
        }
    }
}
