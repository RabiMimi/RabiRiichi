using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace RabiRiichi.Tests.Server.Agents {
  /// <summary>
  /// Plays complete bot-only games end to end. The unit tests above pin down
  /// individual decisions; this one exists to catch the failures they cannot -
  /// a decision path that throws on a shape no fixture happens to build, an
  /// agent that stalls the engine, or an evaluation cost that quietly makes a
  /// turn too slow for the server's action timeout.
  /// </summary>
  [TestClass]
  public class RuleBasedSmokeTest {
    private static Room BuildRoom(int minHan, int playerCount = 4) {
      var config = new GameConfig {
        playerCount = playerCount,
        minHan = minHan,
        // Keep the wall deterministic per run so a failure is reproducible.
        seed = 20260728,
      };
      var room = new Room(new Random(1), config);
      for (int i = 0; i < playerCount; i++) {
        Assert.IsTrue(room.AddPlayer(new RuleBasedAI(-101 - i, room, UserStatus.InRoom)));
      }
      return room;
    }

    private static (Game game, TimeSpan elapsed) PlayOneHand(int minHan) {
      var room = BuildRoom(minHan);
      Game played = null;
      var watch = Stopwatch.StartNew();
      using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
      bool ran = room.RunHeadlessArenaMatch(
          $"smoke-{minHan}", g => played = g, null, cts.Token).GetAwaiter().GetResult();
      watch.Stop();

      Assert.IsTrue(ran, "the headless match refused to start");
      Assert.IsNotNull(played);
      return (played, watch.Elapsed);
    }

    [TestMethod]
    [Timeout(180_000)]
    public void FourBotsFinishAGameOnAOneHanTable() {
      var (game, elapsed) = PlayOneHand(minHan: 1);

      Assert.AreEqual(GamePhase.Finished, game.info.phase);
      // Points are only ever moved between seats, never created or destroyed.
      Assert.AreEqual(
          game.config.pointThreshold.initialPoints * game.config.playerCount,
          game.players.Sum(p => p.points) + game.info.riichiStick * game.config.pointThreshold.riichiPoints);
      Assert.IsTrue(elapsed < TimeSpan.FromMinutes(1),
          $"a bot-only game took {elapsed.TotalSeconds:0.0}s, which is too slow " +
          "for the server's per-action timeout");
    }

    [TestMethod]
    [Timeout(180_000)]
    public void FourBotsFinishAGameOnAHighMinHanTable() {
      // A three-han table is where min-han reasoning matters most: most hands can
      // never legally win, so the agents must keep making decisions instead of
      // deadlocking or throwing on a hand they have priced at zero.
      var (game, elapsed) = PlayOneHand(minHan: 3);

      Assert.AreEqual(GamePhase.Finished, game.info.phase);
      Assert.IsTrue(elapsed < TimeSpan.FromMinutes(1),
          $"a bot-only game took {elapsed.TotalSeconds:0.0}s");
    }

    [TestMethod]
    [Timeout(180_000)]
    public void ThreePlayerBotsFinishAGame() {
      var room = BuildRoom(minHan: 1, playerCount: 3);
      Game played = null;
      using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
      Assert.IsTrue(room.RunHeadlessArenaMatch(
          "smoke-sanma", g => played = g, null, cts.Token).GetAwaiter().GetResult());
      Assert.AreEqual(GamePhase.Finished, played.info.phase);
    }
  }
}
