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
    [TestMethod]
    public void TestStartNewGameAfterGameEnds() {
      var rand = new Random(0);
      var config = new GameConfig { playerCount = 4 };
      var room = new Room(rand, config);

      var user = new User { id = 1, nickname = "Human" };
      Assert.IsTrue(room.AddPlayer(user));
      Assert.IsTrue(room.GetReady(user));

      for (int i = 0; i < 3; i++) {
        var ai = new DefaultAI(-1 - i, room, UserStatus.InRoom);
        Assert.IsTrue(room.AddPlayer(ai));
        Assert.IsTrue(room.GetReady(ai));
      }

      // Game should have started
      Assert.IsNotNull(room.game);
      Assert.AreEqual(UserStatus.Playing, user.status);

      // Invoke private TryEndGame via reflection
      var tryEndGameMethod = typeof(Room).GetMethod("TryEndGame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      Assert.IsNotNull(tryEndGameMethod);
      var result = (bool)tryEndGameMethod.Invoke(room, null);
      Assert.IsTrue(result);

      // After game ends, human should be InRoom
      Assert.AreEqual(UserStatus.InRoom, user.status);
      
      // AIs should be Ready (with my proposed fix)
      var AIs = room.players.Where(p => p is not User).ToList();
      foreach (var ai in AIs) {
        Assert.AreEqual(UserStatus.Ready, ai.status);
      }

      // Human gets ready again
      Assert.IsTrue(room.GetReady(user));

      // Game should start again
      Assert.IsNotNull(room.game);
      Assert.AreEqual(UserStatus.Playing, user.status);
    }

    [TestMethod]
    public void TestRemoveRoomPlayerBeforeGameStarts() {
      var rand = new Random(0);
      var config = new GameConfig { playerCount = 4 };
      var room = new Room(rand, config);

      var user = new User { id = 1, nickname = "Human" };
      Assert.IsTrue(room.AddPlayer(user));

      var ai = new DefaultAI(-100, room, UserStatus.InRoom);
      Assert.IsTrue(room.AddPlayer(ai));
      Assert.AreEqual(2, room.players.Count);

      // The AI can be removed before the game starts.
      Assert.IsTrue(room.RemoveRoomPlayer(ai));
      Assert.IsFalse(room.players.Contains(ai));
      Assert.AreEqual(1, room.players.Count);
    }

    [TestMethod]
    public void TestRemoveRoomPlayerRejectsHumanPlayer() {
      var rand = new Random(0);
      var config = new GameConfig { playerCount = 4 };
      var room = new Room(rand, config);

      var user = new User { id = 1, nickname = "Human" };
      Assert.IsTrue(room.AddPlayer(user));

      // RemoveRoomPlayer must not remove a human player.
      Assert.IsFalse(room.RemoveRoomPlayer(user));
      Assert.IsTrue(room.players.Contains(user));
    }

    [TestMethod]
    public void TestRemoveRoomPlayerRejectedAfterGameStarts() {
      var rand = new Random(0);
      var config = new GameConfig { playerCount = 4 };
      var room = new Room(rand, config);

      var user = new User { id = 1, nickname = "Human" };
      Assert.IsTrue(room.AddPlayer(user));
      Assert.IsTrue(room.GetReady(user));

      DefaultAI lastAi = null;
      for (int i = 0; i < 3; i++) {
        lastAi = new DefaultAI(-1 - i, room, UserStatus.InRoom);
        Assert.IsTrue(room.AddPlayer(lastAi));
        Assert.IsTrue(room.GetReady(lastAi));
      }

      // Game has started; players can no longer be kicked.
      Assert.IsNotNull(room.game);
      Assert.IsFalse(room.RemoveRoomPlayer(lastAi));
      Assert.IsTrue(room.players.Contains(lastAi));
    }
  }
}
