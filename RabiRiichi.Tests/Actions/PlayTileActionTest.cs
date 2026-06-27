using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
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
      Assert.AreEqual(2, win6s.remainingCount);

      var win6p = candidate7s.tenpaiInfos.FirstOrDefault(t => t.winningTile.ToString() == "6p");
      Assert.IsNotNull(win6p);
      Assert.AreEqual(2, win6p.remainingCount);

      // Verify discarding 6s (leaves hand 12367s 234m 34566p waiting on 5s/8s ryanmen)
      var candidate6s = playTileAction.candidates.FirstOrDefault(c => c.tile.tile.ToString() == "6s");
      Assert.IsNotNull(candidate6s);
      Assert.IsTrue(candidate6s.tenpaiInfos.Count > 0, "6s discard should lead to tenpai");
      
      var win5s = candidate6s.tenpaiInfos.FirstOrDefault(t => t.winningTile.ToString() == "5s");
      Assert.IsNotNull(win5s);
      Assert.AreEqual(4, win5s.remainingCount);
    }
  }
}
