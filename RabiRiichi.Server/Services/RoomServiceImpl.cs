using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Services {
  [Authorize]
  public class RoomServiceImpl(ILogger<RoomServiceImpl> logger, RoomTaskQueue taskQueue, Random rand) : RoomService.RoomServiceBase {
    private readonly ILogger<RoomServiceImpl> logger = logger;
    private readonly RoomTaskQueue taskQueue = taskQueue;
    private readonly Random rand = rand;

    public ServerRoomStateResponse CreateRoom(RoomList roomList, User user) {
      var room = new Room(rand, new GameConfig());
      return roomList.Add(room) && room.AddPlayer(user)
        ? new ServerRoomStateResponse {
          State = room.CreateServerRoomStateMsg()
        }
        : throw new RpcException(new Status(StatusCode.Internal, "Cannot add room or join room"));
    }

    public override Task<ServerRoomStateResponse> CreateRoom(Empty request, ServerCallContext context) {
      return taskQueue.Execute(queue => {
        var user = queue.userList.Fetch(context);
        return CreateRoom(queue.roomList, user);
      });
    }

    public ServerRoomStateResponse JoinRoom(JoinRoomRequest request, RoomList roomList, User user) {
      if (!roomList.TryGet(request.RoomId, out var room)) {
        throw new RpcException(
            new Status(StatusCode.NotFound, "Cannot find room"));
      }
      return !room.AddPlayer(user)
        ? throw new RpcException(
            new Status(StatusCode.Unavailable, "Room is full"))
        : new ServerRoomStateResponse {
          State = room.CreateServerRoomStateMsg()
        };
    }

    public override Task<ServerRoomStateResponse> JoinRoom(JoinRoomRequest request, ServerCallContext context) {
      return taskQueue.Execute(queue => {
        var user = queue.userList.Fetch(context);
        return JoinRoom(request, queue.roomList, user);
      });
    }
  }
}