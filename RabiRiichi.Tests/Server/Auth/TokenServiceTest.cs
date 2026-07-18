using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Server.Auth;
using System;

namespace RabiRiichi.Tests.Server.Auth {
  [TestClass]
  public class TokenServiceTest {
    [ClassInitialize]
    public static void ClassInit(TestContext context) {
      Environment.SetEnvironmentVariable("JWT_SECRET", "super_secret_key_for_testing_purposes_only_and_it_is_long_enough");
    }

    [TestMethod]
    public void TestBuildAndValidateToken() {
      var service = new TokenService();
      int userId = 123;
      var token = service.BuildToken(userId);
      Assert.IsFalse(string.IsNullOrEmpty(token));

      Assert.IsTrue(service.IsTokenValid(token, out int validatedUserId));
      Assert.AreEqual(userId, validatedUserId);
    }

    [TestMethod]
    public void TestInvalidToken() {
      var service = new TokenService();
      Assert.IsFalse(service.IsTokenValid("invalid_token", out int validatedUserId));
      Assert.AreEqual(-1, validatedUserId);
    }

    [TestMethod]
    public void TestTokenFromPreviousServerInstanceIsInvalid() {
      var previousServer = new TokenService();
      var restartedServer = new TokenService();
      var token = previousServer.BuildToken(123);

      Assert.IsTrue(previousServer.IsTokenValid(token, out var originalUserId));
      Assert.AreEqual(123, originalUserId);
      Assert.IsFalse(restartedServer.IsTokenValid(token, out var restartedUserId));
      Assert.AreEqual(-1, restartedUserId);
    }
  }
}
