using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Setup;
using System.Text.Json;

namespace RabiRiichi.Server.Services {
  [Authorize]
  public class RoomServiceImpl(ILogger<RoomServiceImpl> logger, RoomTaskQueue taskQueue, Random rand, ReplayStore replayStore, LlmValidator llmValidator) : RoomService.RoomServiceBase {
    private readonly ILogger<RoomServiceImpl> logger = logger;
    private readonly RoomTaskQueue taskQueue = taskQueue;
    private readonly Random rand = rand;
    private readonly ReplayStore replayStore = replayStore;
    private readonly LlmValidator llmValidator = llmValidator;

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
      var room = new Room(rand, config, replayStore);
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
      if (user.room != null) {
        if (user.room.id == room.id) {
          return new ServerRoomStateResponse {
            State = room.CreateServerRoomStateMsg()
          };
        } else {
          throw new RpcException(
              new Status(StatusCode.FailedPrecondition, "Player is already in another room"));
        }
      }
      if (room.players.Count >= room.config.playerCount) {
        throw new RpcException(
            new Status(StatusCode.ResourceExhausted, "Room is full"));
      }
      if (!room.AddPlayer(user)) {
        throw new RpcException(
            new Status(StatusCode.Internal, "Failed to join room"));
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

    /// <summary>
    /// Synchronous room mutation to admit an AI. For LLM AIs, pass the already
    /// validated <paramref name="llmSettings"/> (validation must happen OUTSIDE
    /// the serialized task queue, since it makes a network call). Must be called
    /// on the task-queue thread.
    /// </summary>
    public ServerRoomStateResponse AddAi(
        AddAiRequest request, RoomList roomList, User user, LlmSettings llmSettings = null) {
      var room = user.room ?? throw new RpcException(new Status(StatusCode.FailedPrecondition, "User is not in a room"));
      var humanPlayers = room.players.OfType<User>().OrderBy(p => room.SeatIndexOf(p)).ToList();
      if (humanPlayers.Count == 0 || humanPlayers[0] != user) {
        throw new RpcException(new Status(StatusCode.PermissionDenied, "Only the room owner can add AI"));
      }
      int aiId = room.AllocateAiId();

      IPlayerAgent ai;
      if (request.Type == AiType.Llm) {
        if (llmSettings == null) {
          throw new RpcException(new Status(StatusCode.InvalidArgument, "LLM config missing"));
        }
        ai = new LlmAI(aiId, room, llmSettings, UserStatus.InRoom);
      } else if (AiCreators.TryGetValue(request.Type, out var creator)) {
        ai = creator(aiId, room);
      } else {
        throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid or unsupported AI type: {request.Type}"));
      }

      if (!room.AddPlayer(ai)) {
        throw new RpcException(new Status(StatusCode.Internal, "Cannot add AI to room"));
      }
      room.GetReady(ai);
      return new ServerRoomStateResponse {
        State = room.CreateServerRoomStateMsg()
      };
    }

    /// <summary>
    /// Full AddAi flow used by the WebSocket handler. LLM validation (a network
    /// call) runs OUTSIDE the task queue to avoid stalling all rooms; only the
    /// room mutation is queued.
    /// </summary>
    public async Task<ServerRoomStateResponse> AddAiAsync(AddAiRequest request, User user) {
      LlmSettings llmSettings = null;
      if (request.Type == AiType.Llm) {
        llmSettings = ParseAndValidateLlmConfig(request);
        await ValidateLlmLiveAsync(llmSettings);
      }
      return await taskQueue.Execute(queue => AddAi(request, queue.roomList, user, llmSettings));
    }

    /// <summary> Static (non-network) validation + normalization of LLM config. </summary>
    private static LlmSettings ParseAndValidateLlmConfig(AddAiRequest request) {
      var settings = LlmSettings.FromProto(request.LlmConfig, out var error);
      if (settings == null) {
        throw new RpcException(new Status(StatusCode.InvalidArgument,
            LlmErrorPayload($"error.lobby.llm.config.{error}")));
      }
      return settings;
    }

    /// <summary> Live validation: one small ping to the provider. </summary>
    private async Task ValidateLlmLiveAsync(LlmSettings settings) {
      var result = await llmValidator.ValidateAsync(settings);
      if (!result.Ok) {
        throw new RpcException(new Status(StatusCode.InvalidArgument,
            LlmErrorPayload($"error.lobby.llm.{result.Reason}")));
      }
    }

    /// <summary> Serializes an i18n error payload the client localizes. </summary>
    private static string LlmErrorPayload(string key) {
      return JsonSerializer.Serialize(new { key, @params = new { } });
    }

    public override Task<ServerRoomStateResponse> AddAi(AddAiRequest request, ServerCallContext context) {
      // gRPC path (dormant): validate then mutate. Kept for parity.
      return AddAiAsync(request, taskQueue.userList.Fetch(context));
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
