using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Generated.Messages;
using System;
using System.Linq;

namespace RabiRiichi.Tests.Server {
  [TestClass]
  public class RoomTest {
    [TestMethod]
    public void TestGameStartsWhenAiAddedLast() {
      var rand = new Random(0);
      var config = new GameConfig { playerCount = 4 };
      var room = new Room(rand, config);

      var user = new User { id = 1, nickname = "Human" };

      // Add human player
      Assert.IsTrue(room.AddPlayer(user));
      Assert.AreEqual(UserStatus.InRoom, user.status);

      // Human player gets ready
      Assert.IsTrue(room.GetReady(user));
      Assert.AreEqual(UserStatus.Ready, user.status);
      Assert.IsNull(room.game); // Game should not start yet

      // Add 3 AIs (they are created as InRoom and then readied)
      for (int i = 0; i < 3; i++) {
        var ai = new DefaultAI(-1 - i, room, UserStatus.InRoom);
        Assert.IsTrue(room.AddPlayer(ai));
        Assert.IsTrue(room.GetReady(ai));
      }

      // Now all players are ready, game should have started
      Assert.IsNotNull(room.game);
      Assert.AreEqual(UserStatus.Playing, user.status);
      foreach (var player in room.players) {
        Assert.AreEqual(UserStatus.Playing, player.status);
      }
    }

    [TestMethod]
    public void TestGameStartsWhenHumanReadyLast() {
      var rand = new Random(0);
      var config = new GameConfig { playerCount = 4 };
      var room = new Room(rand, config);

      var user = new User { id = 1, nickname = "Human" };

      // Add 3 AIs (they are created as InRoom and then readied)
      for (int i = 0; i < 3; i++) {
        var ai = new DefaultAI(-1 - i, room, UserStatus.InRoom);
        Assert.IsTrue(room.AddPlayer(ai));
        Assert.IsTrue(room.GetReady(ai));
      }

      Assert.IsNull(room.game); // Game should not start yet

      // Add human player
      Assert.IsTrue(room.AddPlayer(user));
      Assert.AreEqual(UserStatus.InRoom, user.status);
      Assert.IsNull(room.game); // Game should not start yet

      // Human player gets ready
      Assert.IsTrue(room.GetReady(user));

      // Now all players are ready, game should have started
      Assert.IsNotNull(room.game);
      Assert.AreEqual(UserStatus.Playing, user.status);
      foreach (var player in room.players) {
        Assert.AreEqual(UserStatus.Playing, player.status);
      }
    }
  }
}
