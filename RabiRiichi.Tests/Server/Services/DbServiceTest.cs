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

    [TestMethod]
    public void TestNewUserHasZeroTokenVersion() {
      string error;
      int uid = dbService.CreateUser("carol", new UserData { Nickname = "Carol" }, "pw", out error);
      Assert.IsNull(error);
      Assert.AreEqual(0, dbService.GetUserById(uid).TokenVersion);
    }

    [TestMethod]
    public void TestUpdateNickname() {
      string error;
      int uid = dbService.CreateUser("dave", new UserData { Nickname = "Dave" }, "pw", out error);
      Assert.IsNull(error);

      Assert.IsTrue(dbService.UpdateNickname(uid, "  Dave2  ", out error));
      Assert.IsNull(error);
      Assert.AreEqual("Dave2", dbService.GetUserById(uid).UserData.Nickname);

      // Empty nickname is rejected.
      Assert.IsFalse(dbService.UpdateNickname(uid, "   ", out error));
      Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TestChangePasswordBumpsTokenVersionAndRotatesCredential() {
      string error;
      int uid = dbService.CreateUser("erin", new UserData { Nickname = "Erin" }, "old-hash", out error);
      Assert.IsNull(error);

      // Wrong old password is rejected and leaves the version untouched.
      Assert.IsNull(dbService.ChangePassword("erin", "wrong", "new-hash", out error));
      Assert.IsNotNull(error);
      Assert.AreEqual(0, dbService.GetUserById(uid).TokenVersion);

      // Correct old password succeeds and bumps the version.
      var changed = dbService.ChangePassword("erin", "old-hash", "new-hash", out error);
      Assert.IsNull(error);
      Assert.IsNotNull(changed);
      Assert.AreEqual(1, changed.TokenVersion);
      Assert.AreEqual(1, dbService.GetUserById(uid).TokenVersion);

      // Old password no longer authenticates; new one does.
      Assert.IsNull(dbService.AuthenticateUser("erin", "old-hash", out error));
      Assert.IsNotNull(dbService.AuthenticateUser("erin", "new-hash", out error));
    }
  }
}
