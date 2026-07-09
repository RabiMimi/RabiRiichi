using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Core;
using RabiRiichi.Server.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Communication;
using RabiRiichi.Communication.Proto;
using RabiRiichi.Events;
using RabiRiichi.Events.InGame;

namespace RabiRiichi.Tests.Server.Services {
  [TestClass]
  public class ReplayStoreTest {
    private string tempDir;

    [TestInitialize]
    public void SetUp() {
      tempDir = Path.Combine(Path.GetTempPath(), "RabiRiichiReplayTest_" + Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void TearDown() {
      if (Directory.Exists(tempDir)) {
        Directory.Delete(tempDir, true);
      }
    }

    [TestMethod]
    public void TestReplayOptionsDefault() {
      var options = new ReplayOptions();
      Assert.IsNotNull(options);
    }

    [TestMethod]
    public void TestSaveAndGetReplay() {
      var options = new ReplayOptions(tempDir, null);
      var store = new ReplayStore(options);

      Assert.IsTrue(store.IsEnabled);

      var gameId = "20260709T120000-1";
      var log = new GameLogMsg {
        GameId = gameId,
        CreatedAtUnixMs = 1234567890,
      };

      store.SaveReplay(gameId, log);

      var loaded = store.GetReplay(gameId);
      Assert.IsNotNull(loaded);
      Assert.AreEqual(gameId, loaded.GameId);
      Assert.AreEqual(1234567890, loaded.CreatedAtUnixMs);
    }

    [TestMethod]
    public void TestGetReplay_NotFound() {
      var options = new ReplayOptions(tempDir, null);
      var store = new ReplayStore(options);

      var loaded = store.GetReplay("nonexistent");
      Assert.IsNull(loaded);
    }

    [TestMethod]
    public void TestGetReplay_InvalidId() {
      var options = new ReplayOptions(tempDir, null);
      var store = new ReplayStore(options);

      var loaded = store.GetReplay("../nonexistent");
      Assert.IsNull(loaded);

      Assert.ThrowsException<ArgumentException>(() => store.SaveReplay("../invalid", new GameLogMsg()));
    }

    [TestMethod]
    public void TestValidation() {
      Assert.IsTrue(ReplayStore.IsValidGameId("20260709T120000-1"));
      Assert.IsTrue(ReplayStore.IsValidGameId("valid-id"));
      Assert.IsFalse(ReplayStore.IsValidGameId("../invalid"));
      Assert.IsFalse(ReplayStore.IsValidGameId("invalid/id"));
      Assert.IsFalse(ReplayStore.IsValidGameId(""));
      Assert.IsFalse(ReplayStore.IsValidGameId(null));
    }

    [TestMethod]
    public async Task TestCleanupService_DeletesOldFiles() {
      var options = new ReplayOptions(tempDir, 1);
      
      Directory.CreateDirectory(tempDir);
      var file1 = Path.Combine(tempDir, "old-game.pb");
      var file2 = Path.Combine(tempDir, "new-game.pb");
      
      await File.WriteAllTextAsync(file1, "dummy content");
      await File.WriteAllTextAsync(file2, "dummy content");
      
      File.SetLastWriteTimeUtc(file1, DateTime.UtcNow.AddSeconds(-5));
      
      var service = new ReplayCleanupService(options, NullLogger<ReplayCleanupService>.Instance);
      
      await service.StartAsync(default);
      await service.StopAsync(default);
      
      Assert.IsFalse(File.Exists(file1), "Old file should be deleted");
      Assert.IsTrue(File.Exists(file2), "New file should be kept");
    }

    [TestMethod]
    public async Task TestGodViewTeeCapturesNonZeroSeatPrivateEvents() {
      // Regression: the replay tee must capture per-seat [RabiPrivate] events
      // (e.g. furiten) owned by seats other than 0. The old seat-0-only capture
      // dropped them because EventBroadcast only delivers private events to the
      // owner seat.
      var config = new GameConfig();
      var mockActionCenter = new Mock<IActionCenter>();
      config.actionCenter = mockActionCenter.Object;
      var game = new Game(config);

      game.GetPlayer(1).Reset();

      var captured = new List<EventBase>();
      game.onGodViewEvent += ev => captured.Add(ev);

      // A furiten event owned by seat 1 (class-level [RabiPrivate] via
      // PrivatePlayerEvent) is delivered only to seat 1, never seat 0.
      var furitenEvent = new SetTempFuritenEvent(game.initialEvent, 1, true);
      await game.eventBus.Process(furitenEvent, true);

      Assert.IsTrue(captured.Contains(furitenEvent),
          "God-view tee should capture a seat-1 private furiten event.");
    }

    [TestMethod]
    public void TestGodViewSerialization() {
      var config = new GameConfig();
      var mockActionCenter = new Mock<IActionCenter>();
      config.actionCenter = mockActionCenter.Object;
      
      var game = new Game(config);
      var player1 = game.GetPlayer(1);
      player1.Reset();
      
      var tile = new GameTile(new Tile(TileSuit.M, 1), 1);
      tile.player = player1;
      player1.hand.freeTiles.Add(tile);
      
      var tileMsgForPlayer0 = game.SerializeProto<GameTileMsg>(tile, 0);
      var tileMsgForPlayer1 = game.SerializeProto<GameTileMsg>(tile, 1);
      var tileMsgForGod = game.SerializeProto<GameTileMsg>(tile, ProtoConverters.GOD_VIEW_PLAYER_ID);
      
      Assert.IsNotNull(tileMsgForPlayer0);
      Assert.IsNotNull(tileMsgForPlayer1);
      Assert.IsNotNull(tileMsgForGod);
      
      Assert.AreEqual(0, tileMsgForPlayer0.Tile, "Player 0 should see Empty tile for Player 1's hand");
      Assert.AreEqual((int)tile.tile.Val, tileMsgForPlayer1.Tile, "Player 1 should see their own tile");
      Assert.AreEqual((int)tile.tile.Val, tileMsgForGod.Tile, "God view should see Player 1's tile");
    }
  }
}
