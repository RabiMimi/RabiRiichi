using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events.InGame;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Tests {
  [TestClass]
  public class ScenarioNextRoundAckTest {
    [TestMethod]
    public async Task NextRoundAck_ManualAck() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config
              .SetNextRoundAckTimeout(15.0))
          .WithState(state => state.SetRound(Wind.E, 0, 0))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("1112223334445m")
              .SetDiscarded(10, "3s")
              .SetPoints(100000))
          .WithPlayer(1, playerBuilder => playerBuilder
              .SetFreeTiles("1112345678999p")
              .SetDiscarded(10, "4s")
              .SetPoints(100000))
          .WithPlayer(2, playerBuilder => playerBuilder
              .SetFreeTiles("111234567899s7z")
              .SetDiscarded(10, "5s")
              .SetPoints(100000))
          .WithPlayer(3, playerBuilder => playerBuilder
              .SetFreeTiles("1112223334457z")
              .SetDiscarded(10, "6s")
              .SetPoints(100000))
          .WithWall(wall => wall.Reserve("9p"))
          .Build(1)
          .ForceHaitei()
          .Start();

      // Disable auto-ack so we can manually assert and finish it!
      scenario.WithGame(game => {
        var actionCenter = (ScenarioActionCenter)game.config.actionCenter;
        actionCenter.autoAckNextRound = false;
      });

      // Wait for player 1's Tsumo turn
      var inquiry = await scenario.WaitInquiry();

      inquiry.ForPlayer(1, playerInquiry => {
        playerInquiry.ApplyAction<TsumoAction>();
      }).AssertAutoFinish();

      // Wait for the next round ack inquiry!
      var ackInquiry = await scenario.WaitInquiry();

      // Assert that every player gets a NextRoundAction choice
      for (int i = 0; i < 4; i++) {
        ackInquiry.ForPlayer(i, playerInquiry => {
          playerInquiry.AssertAction<NextRoundAction>();
        });
      }

      // Complete acknowledgement for all players
      ackInquiry.ForPlayer(0, p => p.ApplyAction<NextRoundAction>());
      ackInquiry.ForPlayer(1, p => p.ApplyAction<NextRoundAction>());
      ackInquiry.ForPlayer(2, p => p.ApplyAction<NextRoundAction>());
      ackInquiry.ForPlayer(3, p => p.ApplyAction<NextRoundAction>());
      ackInquiry.Finish();

      // Wait for the next game's dealer first turn inquiry to ensure it proceeded
      var nextGameInquiry = await scenario.WaitInquiry();
      Assert.IsNotNull(nextGameInquiry);
    }

    [TestMethod]
    public async Task NextRoundAck_TimeoutProceeds() {
      // Set a short timeout (0.5 seconds) for testing
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config
              .SetNextRoundAckTimeout(0.5))
          .WithState(state => state.SetRound(Wind.E, 0, 0))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("1112223334445m")
              .SetDiscarded(10, "3s")
              .SetPoints(100000))
          .WithPlayer(1, playerBuilder => playerBuilder
              .SetFreeTiles("1112345678999p")
              .SetDiscarded(10, "4s")
              .SetPoints(100000))
          .WithPlayer(2, playerBuilder => playerBuilder
              .SetFreeTiles("111234567899s7z")
              .SetDiscarded(10, "5s")
              .SetPoints(100000))
          .WithPlayer(3, playerBuilder => playerBuilder
              .SetFreeTiles("1112223334457z")
              .SetDiscarded(10, "6s")
              .SetPoints(100000))
          .WithWall(wall => wall.Reserve("9p"))
          .Build(1)
          .ForceHaitei()
          .Start();

      // Disable auto-ack to simulate players not responding
      scenario.WithGame(game => {
        var actionCenter = (ScenarioActionCenter)game.config.actionCenter;
        actionCenter.autoAckNextRound = false;
      });

      // Wait for player 1's Tsumo turn
      var inquiry = await scenario.WaitInquiry();

      inquiry.ForPlayer(1, playerInquiry => {
        playerInquiry.ApplyAction<TsumoAction>();
      }).AssertAutoFinish();

      // Wait for the next round ack inquiry (happens instantly)
      var ackInquiry = await scenario.WaitInquiry();

      // We do NOT acknowledge the next round.
      // We wait for the inquiry to finish (timeout)
      var stopwatch = Stopwatch.StartNew();
      await ackInquiry.inquiry.WaitForFinish;

      // Now the inquiry has finished (via timeout), so the game loop has proceeded to the next round.
      // Wait for the dealer first turn inquiry of the new round.
      var nextGameInquiry = await scenario.WaitInquiry();
      stopwatch.Stop();

      Assert.IsNotNull(nextGameInquiry);
      // Ensure it waited at least close to 0.5 seconds
      Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 400, $"Elapsed was only {stopwatch.ElapsedMilliseconds}ms");
    }
  }
}
