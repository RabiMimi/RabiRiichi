using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Services {
    [Authorize]
    public class RoomServiceImpl : RoomService.RoomServiceBase {
        private readonly ILogger<RoomServiceImpl> logger;
        private readonly RoomList roomList;
        private readonly UserList userList;
        private readonly Random rand;

        public RoomServiceImpl(ILogger<RoomServiceImpl> logger, RoomList roomList, UserList userList, Random rand) {
            this.logger = logger;
            this.roomList = roomList;
            this.userList = userList;
            this.rand = rand;
        }

        public Task<JoinRoomResponse> CreateRoom(User user) {
            var room = new Room(rand, new GameConfig());
            if (roomList.Add(room) && room.AddPlayer(user)) {
                return Task.FromResult(new JoinRoomResponse {
                    RoomId = room.id,
                    RoomState = room.GetServerRoomStateMsg(),
                });
            }
            return Task.FromException<JoinRoomResponse>(new RpcException(new Status(StatusCode.Internal, "Cannot add room or join room")));
        }

        public override Task<JoinRoomResponse> CreateRoom(Empty request, ServerCallContext context) {
            var user = userList.Fetch(context);
            return CreateRoom(user);
        }

        public Task<JoinRoomResponse> JoinRoom(JoinRoomRequest request, User user) {
            if (!roomList.TryGet(request.RoomId, out var room)) {
                throw new RpcException(
                    new Status(StatusCode.NotFound, "Cannot find room"));
            }
            if (!room.AddPlayer(user)) {
                throw new RpcException(
                    new Status(StatusCode.Unavailable, "Room is full"));
            }
            return Task.FromResult(new JoinRoomResponse {
                RoomId = room.id,
                RoomState = room.GetServerRoomStateMsg(),
            });
        }

        public override Task<JoinRoomResponse> JoinRoom(JoinRoomRequest request, ServerCallContext context) {
            var user = userList.Fetch(context);
            return JoinRoom(request, user);
        }
    }
}