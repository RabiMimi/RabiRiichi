using Grpc.Core;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Services {
    public class GameServiceImpl : GameService.GameServiceBase {
        private readonly ILogger<GameServiceImpl> logger;
        private readonly RoomTaskQueue taskQueue;

        public GameServiceImpl(ILogger<GameServiceImpl> logger, RoomTaskQueue taskQueue) {
            this.logger = logger;
            this.taskQueue = taskQueue;
        }

        public override async Task ConnectGame(IAsyncStreamReader<ClientMessageDto> requestStream, IServerStreamWriter<ServerMessageDto> responseStream, ServerCallContext context) {
            var (user, rabiCtx) = await taskQueue.Execute(queue => {
                var user = queue.userList.Fetch(context);
                return (user, user?.Connect(requestStream, responseStream));
            });
            if (rabiCtx == null) {
                return;
            }
            if (!await rabiCtx.HandShake()) {
                return;
            }
            await taskQueue.Execute(() => {
                user.room?.BroadcastRoomState();
            });
            try {
                await Task.Delay(TimeSpan.FromDays(7), rabiCtx.cts.Token);
            } catch (OperationCanceledException) { }
        }
    }
}
