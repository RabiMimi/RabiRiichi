using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Services {
  public class UserServiceImpl(ILogger<UserServiceImpl> logger, RoomTaskQueue taskQueue, TokenService tokenService, DbService dbService) : UserService.UserServiceBase {
    private readonly ILogger<UserServiceImpl> logger = logger;
    private readonly RoomTaskQueue taskQueue = taskQueue;
    private readonly TokenService tokenService = tokenService;
    private readonly DbService dbService = dbService;

    public UserInfoResponse CreateUser(CreateUserRequest request, UserList userList) {
      if (request.UserData == null || string.IsNullOrWhiteSpace(request.UserData.Nickname)) {
        throw new RpcException(new Status(StatusCode.InvalidArgument, "Nickname cannot be empty"));
      }

      int userId = dbService.CreateUser(request.Username, request.UserData, request.PasswordHash, out var error);
      if (userId == -1) {
        if (error == "Username already exists") {
          throw new RpcException(new Status(StatusCode.AlreadyExists, error));
        }
        if (error.Contains("cannot be empty")) {
          throw new RpcException(new Status(StatusCode.InvalidArgument, error));
        }
        throw new RpcException(new Status(StatusCode.Internal, error ?? "Cannot create user"));
      }

      var user = new User {
        id = userId,
        nickname = request.UserData.Nickname
      };
      if (!userList.Add(user)) {
        throw new RpcException(new Status(StatusCode.Internal, "Cannot add user"));
      }
      user.AddRoomListeners(taskQueue);
      return new UserInfoResponse {
        Id = user.id,
        UserData = request.UserData,
        Status = user.status,
        AccessToken = tokenService.BuildToken(user.id)
      };
    }

    public override Task<UserInfoResponse> CreateUser(CreateUserRequest request, ServerCallContext context) {
      return taskQueue.Execute(queue => {
        return CreateUser(request, queue.userList);
      });
    }

    public UserInfoResponse LoginUser(LoginUserRequest request, UserList userList) {
      var dbUser = dbService.AuthenticateUser(request.Username, request.PasswordHash, out var error)
        ?? throw new RpcException(new Status(StatusCode.Unauthenticated, error ?? "Invalid username or password"));

      if (!userList.TryGet(dbUser.Id, out var user)) {
        user = new User {
          id = dbUser.Id,
          nickname = dbUser.UserData.Nickname
        };
        if (!userList.Add(user)) {
          throw new RpcException(new Status(StatusCode.Internal, "Cannot add user"));
        }
        user.AddRoomListeners(taskQueue);
      }

      return new UserInfoResponse {
        Id = user.id,
        UserData = dbUser.UserData,
        Room = user.room?.CreateServerRoomStateMsg(),
        Status = user.status,
        AccessToken = tokenService.BuildToken(user.id)
      };
    }

    public override Task<UserInfoResponse> LoginUser(LoginUserRequest request, ServerCallContext context) {
      return taskQueue.Execute(queue => {
        return LoginUser(request, queue.userList);
      });
    }

    public UserInfoResponse GetMyInfo(User user) {
      return new UserInfoResponse {
        Id = user.id,
        UserData = new UserData { Nickname = user.nickname },
        Room = user.room?.CreateServerRoomStateMsg(),
        Status = user.status,
      };
    }

    [Authorize]
    public override Task<UserInfoResponse> GetMyInfo(Empty request, ServerCallContext context) {
      return taskQueue.Execute(queue => {
        var user = queue.userList.Fetch(context);
        return GetMyInfo(user);
      });
    }
  }
}
