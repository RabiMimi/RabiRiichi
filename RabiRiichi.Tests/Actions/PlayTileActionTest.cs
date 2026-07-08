using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Tests.Scenario;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Actions {
  [TestClass]
  public class PlayTileActionTest {
    [TestMethod]
    public async Task TestDiscardCandidatesTenpaiInfo() {
      Game game = null;
      var scenario = new ScenarioBuilder()
          .WithPlayer(1, playerBuilder => {
            playerBuilder.SetFreeTiles("12366s234m34566p");
          })
          .WithWall(wall => wall.Reserve("7s")) // Player 1 draws 7s
          .Start(1);

      scenario.WithGame(g => game = g);

      var matcher = await scenario.WaitInquiry();
      var playerInquiry = matcher.inquiry.GetByPlayerId(1);
      Assert.IsNotNull(playerInquiry);

      var playTileAction = (PlayTileAction)playerInquiry.actions.Find(a => a is PlayTileAction);
      Assert.IsNotNull(playTileAction);
      Assert.IsNotNull(playTileAction.candidates);

      // Verify candidate for discarding 7s (leaves hand in Shanpon wait 6s/6p)
      var candidate7s = playTileAction.candidates.FirstOrDefault(c => c.tile.tile.ToString() == "7s");
      Assert.IsNotNull(candidate7s, "7s should be a discard candidate");
      Assert.AreEqual(2, candidate7s.tenpaiInfos.Count, "7s discard should wait on 2 tiles (6s and 6p)");

      var win6s = candidate7s.tenpaiInfos.FirstOrDefault(t => t.winningTile.ToString() == "6s");
      Assert.IsNotNull(win6s);

      var win6p = candidate7s.tenpaiInfos.FirstOrDefault(t => t.winningTile.ToString() == "6p");
      Assert.IsNotNull(win6p);

      // Verify discarding 6s (leaves hand 12367s 234m 34566p waiting on 5s/8s ryanmen)
      var candidate6s = playTileAction.candidates.FirstOrDefault(c => c.tile.tile.ToString() == "6s");
      Assert.IsNotNull(candidate6s);
      Assert.IsTrue(candidate6s.tenpaiInfos.Count > 0, "6s discard should lead to tenpai");

      var win5s = candidate6s.tenpaiInfos.FirstOrDefault(t => t.winningTile.ToString() == "5s");
      Assert.IsNotNull(win5s);
    }

    [TestMethod]
    public async Task TestTenpaiInfoReportsRonFloorNotSuuankou() {
      // Four concealed triplets + shanpon wait. On tsumo this is suuankou
      // (yakuman), but the preview must report the RON floor (sanankou + toitoi),
      // since the player cannot guarantee the tsumo. Also verifies yaku excludes
      // dora and situational yaku (tsumo) are not counted.
      var scenario = new ScenarioBuilder()
          .WithPlayer(1, playerBuilder => {
            playerBuilder.SetFreeTiles("111m222m333p44s55s");
          })
          .WithWall(wall => wall.Reserve("9m")) // unrelated draw
          .Start(1);

      var matcher = await scenario.WaitInquiry();
      var playerInquiry = matcher.inquiry.GetByPlayerId(1);
      var playTileAction = (PlayTileAction)playerInquiry.actions.Find(a => a is PlayTileAction);
      Assert.IsNotNull(playTileAction);

      // Discarding the unrelated 9m keeps the four-triplet shanpon tenpai (4s/5s).
      var candidate = playTileAction.candidates.FirstOrDefault(c => c.tile.tile.ToString() == "9m");
      Assert.IsNotNull(candidate);

      foreach (var info in candidate.tenpaiInfos) {
        Assert.AreEqual(0, info.yakuman,
            $"wait {info.winningTile} must not report suuankou (ron floor)");
        // sanankou (2) + toitoi (2); menzen tsumo is excluded (ron floor).
        Assert.AreEqual(4, info.yaku,
            $"wait {info.winningTile} should be sanankou + toitoi = 4 yaku");
        // yaku excludes dora; han includes any dora, so han >= yaku.
        Assert.IsTrue(info.han >= info.yaku,
            "han (yaku + dora) must be at least the yaku-only count");
      }
    }
  }
}
