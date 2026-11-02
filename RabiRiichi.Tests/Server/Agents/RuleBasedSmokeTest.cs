using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events;
using RabiRiichi.Events.InGame;
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

    /// <summary> A rule-based bot that also counts the discards it sees. </summary>
    private sealed class CountingBot(int id, Room room) : RuleBasedAI(id, room, UserStatus.InRoom) {
      public int Discards { get; private set; }
      public int WildcardDiscards { get; private set; }

      public override void OnEvent(EventBase ev) {
        if (ev is DiscardTileEvent discard) {
          Discards++;
          if (discard.discarded.tile.IsWildcard) {
            WildcardDiscards++;
          }
        }
      }
    }

    /// <summary>
    /// The one other game worth its seconds. A bot only throws a wildcard under
    /// pressure that builds up over a real hand, which no single fixture covers,
    /// and wildcard decks run decision paths the game above never reaches.
    /// </summary>
    [TestMethod]
    [Timeout(60_000)]
    public void BotsNeverThrowAWildcardOverAWholeGame() {
      const int playerCount = 2;
      var config = new GameConfig {
        playerCount = playerCount,
        // A wall where bots that misjudge wildcards really do throw one: most
        // seeds never put them in that spot.
        seed = 20260003,
      };
      config.initialTiles = new Tiles(config.initialTiles) {
        Tile.Wildcard, Tile.Wildcard, Tile.Wildcard, Tile.Wildcard,
      };
      var room = new Room(new Random(3), config);
      var bot = new CountingBot(-101, room);
      Assert.IsTrue(room.AddPlayer(bot));
      Assert.IsTrue(room.AddPlayer(new RuleBasedAI(-102, room, UserStatus.InRoom)));

      Game game = null;
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
      bool ran = room.RunHeadlessArenaMatch(
          "smoke", g => game = g, null, cts.Token).GetAwaiter().GetResult();

      Assert.IsTrue(ran, "the headless match refused to start");
      Assert.AreEqual(GamePhase.Finished, game.info.phase);
      Assert.IsTrue(bot.Discards > 0, "the bot never saw a discard");
      Assert.AreEqual(0, bot.WildcardDiscards, $"out of {bot.Discards} discards");
    }
  }
}
