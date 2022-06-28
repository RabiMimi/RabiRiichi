using Grpc.Core;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Services {
    public class GameServiceImpl : GameService.GameServiceBase {
        private readonly ILogger<GameServiceImpl> logger;
        private readonly UserList userList;

        public GameServiceImpl(ILogger<GameServiceImpl> logger, UserList userList) {
            this.logger = logger;
            this.userList = userList;
        }

        public override async Task ConnectGame(IAsyncStreamReader<ClientMessageDto> requestStream, IServerStreamWriter<ServerMessageDto> responseStream, ServerCallContext context) {
            var user = userList.Fetch(context);
            var rabiCtx = user.Connect(requestStream, responseStream);
            if (rabiCtx == null) {
                return;
            }
            if (!await rabiCtx.HandShake()) {
                return;
            }
            var room = user.room;
            if (room != null) {
                room.BroadcastRoomState();
            }
            try {
                await Task.Delay(TimeSpan.FromDays(7), rabiCtx.cts.Token);
            } catch (OperationCanceledException) { }
        }
    }
}
