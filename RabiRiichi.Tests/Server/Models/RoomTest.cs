using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Generated.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Tests.Server {
  [TestClass]
  public class RoomTest {
    private sealed class ChatRecordingAi(int id, Room room)
        : AIAgent(id, room, UserStatus.InRoom) {
      public readonly List<int> ChatSenders = [];
      public override AiType aiType => AiType.Dummy;

      public override void OnChat(int senderId, string text, string sticker) {
        ChatSenders.Add(senderId);
      }

      protected override InquiryResponse Decide(
          MultiPlayerInquiry gameInquiry,
          SinglePlayerInquiry playerInquiry,
          TimeSpan remainingTimeout) => InquiryResponse.Default(Seat);
    }

    [TestMethod]
    public void AgentChatUsesTheEmittingAgentIdentity() {
      var room = new Room(new Random(0), new GameConfig { playerCount = 4 });
      var gemini = new ChatRecordingAi(-101, room);
      var deepSeek = new ChatRecordingAi(-102, room);
      var observer = new ChatRecordingAi(-103, room);
      Assert.IsTrue(room.AddPlayer(gemini));
      Assert.IsTrue(room.AddPlayer(deepSeek));
      Assert.IsTrue(room.AddPlayer(observer));

      room.SendAgentChat(gemini, "gemini message", null);
      room.SendAgentChat(deepSeek, "deepseek message", null);

      CollectionAssert.AreEqual(new[] { -101, -102 }, observer.ChatSenders);
    }

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
