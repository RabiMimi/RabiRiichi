using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;
using System;
using System.Linq;
using System.Threading;

namespace RabiRiichi.Tests.Server.Agents {
  /// <summary>
  /// Plays one complete bot-only game end to end. The unit tests elsewhere pin
  /// down individual decisions; this exists to catch what they cannot - a
  /// decision path that throws on a shape no fixture happens to build, or an
  /// agent that stalls the engine.
  ///
  /// Deliberately ONE small game. It runs every decision path (draw, discard,
  /// riichi, calls, kan, round transitions) a few hundred times over, and a
  /// second or third game adds coverage far too slowly to be worth the seconds
  /// it costs every developer on every run. A high minimum-han table is chosen
  /// because it is the harshest setting for the agent: most hands can never
  /// legally win, so it must keep deciding rather than deadlock or throw on a
  /// hand it has priced at zero.
  /// </summary>
  [TestClass]
  public class RuleBasedSmokeTest {
    [TestMethod]
    [Timeout(60_000)]
    public void BotsFinishAGameOnAHighMinHanTable() {
      const int playerCount = 2;
      var config = new GameConfig {
        playerCount = playerCount,
        minHan = 3,
        // Fix the wall so a failure here is reproducible.
        seed = 20260728,
      };
      var room = new Room(new Random(1), config);
      for (int i = 0; i < playerCount; i++) {
        Assert.IsTrue(room.AddPlayer(new RuleBasedAI(-101 - i, room, UserStatus.InRoom)));
      }

      Game game = null;
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
      bool ran = room.RunHeadlessArenaMatch(
          "smoke", g => game = g, null, cts.Token).GetAwaiter().GetResult();

      Assert.IsTrue(ran, "the headless match refused to start");
      Assert.IsNotNull(game);
      Assert.AreEqual(GamePhase.Finished, game.info.phase);
      // Points are only ever moved between seats, never created or destroyed.
      Assert.AreEqual(
          config.pointThreshold.initialPoints * playerCount,
          game.players.Sum(p => p.points)
              + game.info.riichiStick * config.pointThreshold.riichiPoints);
    }
  }
}
