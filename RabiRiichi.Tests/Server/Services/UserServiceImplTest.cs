using Microsoft.VisualStudio.TestTools.UnitTesting;
using Grpc.Core;
using RabiRiichi.Server.Auth;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Services;
using System;
using System.IO;

namespace RabiRiichi.Tests.Server.Services {
  [TestClass]
  public class UserServiceImplTest {
    private string dbPath;
    private DbService dbService;
    private UserList userList;
    private UserServiceImpl userService;

    [TestInitialize]
    public void Setup() {
      Environment.SetEnvironmentVariable("JWT_SECRET", "super_secret_key_for_testing_purposes_only_and_it_is_long_enough");
      dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_user_{Guid.NewGuid():N}.db");
      Environment.SetEnvironmentVariable("RABIRIICHI_DB_PATH", dbPath);
      dbService = new DbService();
      dbService.InitializeDatabase();
      userList = new UserList();
      userService = new UserServiceImpl(null, null, new TokenService(), dbService);
    }

    [TestCleanup]
    public void Cleanup() {
      Environment.SetEnvironmentVariable("RABIRIICHI_DB_PATH", null);
      if (File.Exists(dbPath)) {
        try {
          File.Delete(dbPath);
        } catch {
          // ignore
        }
      }
    }

    [TestMethod]
    public void TestLoginUser_UserDoesNotExist_ThrowsUnauthenticated() {
      var request = new LoginUserRequest {
        Username = "nonexistent_user",
        PasswordHash = "somehash"
      };

      var ex = Assert.ThrowsException<RpcException>(() => userService.LoginUser(request, userList));
      Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
      Assert.AreEqual("User does not exist", ex.Status.Detail);
    }

    [TestMethod]
    public void TestLoginUser_PasswordMismatch_ThrowsUnauthenticated() {
      dbService.CreateUser("testplayer", new UserData { Nickname = "TestPlayer" }, "correct_hash", out var err);
      Assert.IsNull(err);

      var request = new LoginUserRequest {
        Username = "testplayer",
        PasswordHash = "wrong_hash"
      };

      var ex = Assert.ThrowsException<RpcException>(() => userService.LoginUser(request, userList));
      Assert.AreEqual(StatusCode.Unauthenticated, ex.StatusCode);
      Assert.AreEqual("Password mismatch", ex.Status.Detail);
    }

    [TestMethod]
    public void TestLoginUser_EmptyUsernameOrPassword_ThrowsInvalidArgument() {
      var emptyUserReq = new LoginUserRequest {
        Username = "",
        PasswordHash = "somehash"
      };
      var ex1 = Assert.ThrowsException<RpcException>(() => userService.LoginUser(emptyUserReq, userList));
      Assert.AreEqual(StatusCode.InvalidArgument, ex1.StatusCode);
      Assert.AreEqual("Username cannot be empty", ex1.Status.Detail);

      var emptyPassReq = new LoginUserRequest {
        Username = "testplayer",
        PasswordHash = ""
      };
      var ex2 = Assert.ThrowsException<RpcException>(() => userService.LoginUser(emptyPassReq, userList));
      Assert.AreEqual(StatusCode.InvalidArgument, ex2.StatusCode);
      Assert.AreEqual("Password cannot be empty", ex2.Status.Detail);
    }

    [TestMethod]
    public void TestLoginUser_Success() {
      dbService.CreateUser("validplayer", new UserData { Nickname = "ValidPlayer" }, "valid_hash", out var err);
      Assert.IsNull(err);

      var request = new LoginUserRequest {
        Username = "validplayer",
        PasswordHash = "valid_hash"
      };

      var response = userService.LoginUser(request, userList);
      Assert.IsNotNull(response);
      Assert.AreEqual("ValidPlayer", response.UserData.Nickname);
      Assert.IsFalse(string.IsNullOrEmpty(response.AccessToken));
    }
  }
}
