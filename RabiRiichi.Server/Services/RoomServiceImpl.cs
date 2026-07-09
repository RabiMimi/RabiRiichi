using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Setup;
using System.Text.Json;

namespace RabiRiichi.Server.Services {
  [Authorize]
  public class RoomServiceImpl(ILogger<RoomServiceImpl> logger, RoomTaskQueue taskQueue, Random rand) : RoomService.RoomServiceBase {
    private readonly ILogger<RoomServiceImpl> logger = logger;
    private readonly RoomTaskQueue taskQueue = taskQueue;
    private readonly Random rand = rand;

    private static readonly Dictionary<AiType, Func<int, Room, IPlayerAgent>> AiCreators = new() {
      { AiType.Dummy, (id, room) => new DefaultAI(id, room, UserStatus.InRoom) },
      { AiType.RuleBased, (id, room) => new RuleBasedAI(id, room, UserStatus.InRoom) },
    };

    public ServerRoomStateResponse CreateRoom(CreateRoomRequest request, RoomList roomList, User user) {
      var config = GameConfig.FromProto(request?.Config);
      try {
        config.Validate();
      } catch (InvalidGameConfigException ex) {
        var payload = new {
          key = MapToI18nKey(ex.ErrorType),
          @params = ex.Parameters
        };
        string json = JsonSerializer.Serialize(payload);
        throw new RpcException(new Status(StatusCode.InvalidArgument, json));
      }
      var allowedYakus = request?.Config?.AllowedYakus?.ToArray();
      config.setup = new DynamicRiichiSetup(allowedYakus, logger);
      var room = new Room(rand, config);
      return roomList.Add(room) && room.AddPlayer(user)
        ? new ServerRoomStateResponse {
          State = room.CreateServerRoomStateMsg()
        }
        : throw new RpcException(new Status(StatusCode.Internal, "Cannot add room or join room"));
    }

    public override Task<ServerRoomStateResponse> CreateRoom(CreateRoomRequest request, ServerCallContext context) {
      return taskQueue.Execute(queue => {
        var user = queue.userList.Fetch(context);
        return CreateRoom(request, queue.roomList, user);
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

    public ServerRoomStateResponse AddAi(AddAiRequest request, RoomList roomList, User user) {
      var room = user.room ?? throw new RpcException(new Status(StatusCode.FailedPrecondition, "User is not in a room"));
      var humanPlayers = room.players.OfType<User>().OrderBy(p => room.SeatIndexOf(p)).ToList();
      if (humanPlayers.Count == 0 || humanPlayers[0] != user) {
        throw new RpcException(new Status(StatusCode.PermissionDenied, "Only the room owner can add AI"));
      }
      if (!AiCreators.TryGetValue(request.Type, out var creator)) {
        throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid or unsupported AI type: {request.Type}"));
      }
      int aiId = -100 - room.players.Count;

      var ai = creator(aiId, room);
      if (!room.AddPlayer(ai)) {
        throw new RpcException(new Status(StatusCode.Internal, "Cannot add AI to room"));
      }
      room.GetReady(ai);
      return new ServerRoomStateResponse {
        State = room.CreateServerRoomStateMsg()
      };
    }

    public override Task<ServerRoomStateResponse> AddAi(AddAiRequest request, ServerCallContext context) {
      return taskQueue.Execute(queue => {
        var user = queue.userList.Fetch(context);
        return AddAi(request, queue.roomList, user);
      });
    }

    public ServerRoomStateResponse RemoveRoomPlayer(RemoveRoomPlayerRequest request, RoomList roomList, User user) {
      var room = user.room ?? throw new RpcException(new Status(StatusCode.FailedPrecondition, "User is not in a room"));
      var humanPlayers = room.players.OfType<User>().OrderBy(p => room.SeatIndexOf(p)).ToList();
      if (humanPlayers.Count == 0 || humanPlayers[0] != user) {
        throw new RpcException(new Status(StatusCode.PermissionDenied, "Only the room owner can remove players"));
      }
      var target = room.players.FirstOrDefault(p => p.id == request.Id)
        ?? throw new RpcException(new Status(StatusCode.NotFound, "Player not found in room"));
      if (!room.RemoveRoomPlayer(target)) {
        throw new RpcException(new Status(StatusCode.FailedPrecondition, "Cannot remove player from room"));
      }
      return new ServerRoomStateResponse {
        State = room.CreateServerRoomStateMsg()
      };
    }

    public override Task<ServerRoomStateResponse> RemoveRoomPlayer(RemoveRoomPlayerRequest request, ServerCallContext context) {
      return taskQueue.Execute(queue => {
        var user = queue.userList.Fetch(context);
        return RemoveRoomPlayer(request, queue.roomList, user);
      });
    }

    private static string MapToI18nKey(GameConfigErrorType errorType) {
      return errorType switch {
        GameConfigErrorType.InvalidPlayerCount => "error.lobby.playerCount",
        GameConfigErrorType.InvalidTotalRound => "error.lobby.totalRound",
        GameConfigErrorType.InvalidMinHan => "error.lobby.minHan",
        GameConfigErrorType.InvalidTimeout => "error.lobby.timeout",
        GameConfigErrorType.InsufficientTiles => "error.lobby.tileSet",
        GameConfigErrorType.InvalidInitialPoints => "error.lobby.initialPoints",
        GameConfigErrorType.InvalidFinishPoints => "error.lobby.finishPoints",
        GameConfigErrorType.InvalidRiichiPoints => "error.lobby.riichiPoints",
        GameConfigErrorType.InvalidHonbaPoints => "error.lobby.honbaPoints",
        GameConfigErrorType.InvalidRyuukyokuPoints => "error.lobby.ryuukyokuPoints",
        GameConfigErrorType.InvalidPointsRange => "error.lobby.pointsRange",
        _ => "error.lobby.generic"
      };
    }
  }
}