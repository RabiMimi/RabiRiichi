using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Server.Services;
using RabiRiichi.Server.Generated.Rpc;
using System;
using System.IO;

namespace RabiRiichi.Tests.Server.Services {
  [TestClass]
  public class DbServiceTest {
    private string dbPath;
    private DbService dbService;

    [TestInitialize]
    public void Setup() {
      dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_{Guid.NewGuid():N}.db");
      Environment.SetEnvironmentVariable("RABIRIICHI_DB_PATH", dbPath);
      dbService = new DbService();
      dbService.InitializeDatabase();
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
    public void TestCreateUserAndAuthenticate() {
      var userData = new UserData { Nickname = "AliceDisplay" };
      string error;
      int uid = dbService.CreateUser("alice", userData, "mypassword123", out error);

      Assert.IsNull(error);
      Assert.IsTrue(uid > 0);

      // Authenticate success
      var authUser = dbService.AuthenticateUser("alice", "mypassword123", out error);
      Assert.IsNull(error);
      Assert.IsNotNull(authUser);
      Assert.AreEqual(uid, authUser.Id);
      Assert.AreEqual("alice", authUser.Username);
      Assert.AreEqual("AliceDisplay", authUser.UserData.Nickname);

      // Authenticate username case-insensitivity
      var authUserCaps = dbService.AuthenticateUser("ALICE", "mypassword123", out error);
      Assert.IsNull(error);
      Assert.IsNotNull(authUserCaps);
      Assert.AreEqual(uid, authUserCaps.Id);

      // Authenticate failure - bad password
      var badPass = dbService.AuthenticateUser("alice", "wrongpassword", out error);
      Assert.IsNotNull(error);
      Assert.IsNull(badPass);

      // Authenticate failure - bad username
      var badUser = dbService.AuthenticateUser("unknown", "mypassword123", out error);
      Assert.IsNotNull(error);
      Assert.IsNull(badUser);
    }

    [TestMethod]
    public void TestCreateUserDuplicateUsername() {
      var u1 = new UserData { Nickname = "User1" };
      var u2 = new UserData { Nickname = "User2" };
      string error;

      int uid1 = dbService.CreateUser("testuser", u1, "pass", out error);
      Assert.IsNull(error);
      Assert.IsTrue(uid1 > 0);

      // Register same username caps
      int uid2 = dbService.CreateUser("TESTUSER", u2, "pass2", out error);
      Assert.IsNotNull(error);
      Assert.AreEqual(-1, uid2);
      Assert.AreEqual("Username already exists", error);
    }

    [TestMethod]
    public void TestGetUserById() {
      var userData = new UserData { Nickname = "BobDisplay" };
      string error;
      int uid = dbService.CreateUser("bob", userData, "password", out error);
      Assert.IsNull(error);

      var user = dbService.GetUserById(uid);
      Assert.IsNotNull(user);
      Assert.AreEqual(uid, user.Id);
      Assert.AreEqual("bob", user.Username);
      Assert.AreEqual("BobDisplay", user.UserData.Nickname);

      var nonExistent = dbService.GetUserById(999999);
      Assert.IsNull(nonExistent);
    }
  }
}
