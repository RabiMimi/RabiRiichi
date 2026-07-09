using Grpc.Core;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Services {
  public class GameServiceImpl(ILogger<GameServiceImpl> logger, RoomTaskQueue taskQueue) : GameService.GameServiceBase {
    private readonly ILogger<GameServiceImpl> logger = logger;
    private readonly RoomTaskQueue taskQueue = taskQueue;

    public override async Task ConnectGame(IAsyncStreamReader<ClientMessageDto> requestStream, IServerStreamWriter<ServerMessageDto> responseStream, ServerCallContext context) {
      var (user, rabiCtx) = await taskQueue.Execute(queue => {
        var user = queue.userList.Fetch(context);
        return (user, user?.connection.Connect(requestStream, responseStream));
      });
      if (rabiCtx == null) {
        return;
      }
      if (!await rabiCtx.HandShake()) {
        rabiCtx.Close();
        return;
      }
      await taskQueue.Execute(() => {
        user.room?.BroadcastRoomState();
        user.room?.SyncGameTo(user);
      });
      try {
        // Wait until the stream closes (its cts) OR the call is cancelled.
        // context.CancellationToken fires on client disconnect AND on host
        // shutdown (Ctrl+C), so this no longer blocks graceful shutdown.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            rabiCtx.cts.Token, context.CancellationToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, linkedCts.Token);
      } catch (OperationCanceledException) { } finally {
        rabiCtx.Close();
      }
    }
  }
}