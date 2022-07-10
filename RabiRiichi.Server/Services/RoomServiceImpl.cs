using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Services {
    [Authorize]
    public class RoomServiceImpl : RoomService.RoomServiceBase {
        private readonly ILogger<RoomServiceImpl> logger;
        private readonly RoomTaskQueue taskQueue;
        private readonly Random rand;

        public RoomServiceImpl(ILogger<RoomServiceImpl> logger, RoomTaskQueue taskQueue, Random rand) {
            this.logger = logger;
            this.taskQueue = taskQueue;
            this.rand = rand;
        }

        public ServerRoomStateResponse CreateRoom(RoomList roomList, User user) {
            var room = new Room(rand, new GameConfig());
            if (roomList.Add(room) && room.AddPlayer(user)) {
                return new ServerRoomStateResponse {
                    State = room.CreateServerRoomStateMsg()
                };
            }
            throw new RpcException(new Status(StatusCode.Internal, "Cannot add room or join room"));
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
            if (!room.AddPlayer(user)) {
                throw new RpcException(
                    new Status(StatusCode.Unavailable, "Room is full"));
            }
            return new ServerRoomStateResponse {
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