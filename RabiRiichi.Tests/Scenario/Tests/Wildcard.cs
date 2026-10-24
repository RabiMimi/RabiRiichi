using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Events.InGame;
using RabiRiichi.Patterns;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Tests {
  [TestClass]
  public class ScenarioWildcard {
    private static Tiles WildcardDeck() =>
        new(Tiles.All.Value.Concat(Enumerable.Repeat(Tile.Wildcard, 4)));

    [TestMethod]
    public async Task DiscardingWildcardWhileTenpaiIsFuriten() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config.SetInitialTiles(WildcardDeck()))
          .WithPlayer(1, player => player.SetFreeTiles("123456789m11z34s"))
          .WithWall(wall => wall.Reserve("*2s*"))
          .Start(1);

      // Player 1 is tenpai on 25s; every wait list on a wildcard table also
      // holds `*`, so throwing a wildcard away is a discard of a winning tile.
      (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
          .ChooseTile<PlayTileAction>("*")
      ).AssertAutoFinish();

      var inquiry = await scenario.WaitInquiry();
      scenario.WithPlayer(1, player => {
        Assert.IsTrue(player.hand.Tenpai.Contains(Tile.Wildcard));
        Assert.IsTrue(player.hand.isDiscardFuriten);
      });

      // Neither a natural winning tile nor another wildcard can be ronned.
      inquiry.Finish(); // player 2 throws the 2s it drew
      (await scenario.WaitInquiry()).AssertNoActionForPlayer(1).Finish(); // player 3 throws a wildcard
      (await scenario.WaitInquiry()).AssertNoActionForPlayer(1);
    }

    [TestMethod]
    public async Task WildcardIndicatorMakesWildcardsDora() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config.SetInitialTiles(WildcardDeck()))
          .WithPlayer(1, player => player.SetFreeTiles("123456789m11z3s*"))
          .WithWall(wall => wall.Reserve("2s").AddDoras("*"))
          .Start(1);

      var inquiry = await scenario.WaitInquiry();
      // Read before winning: the next round has been dealt by the time the
      // agari event is asserted.
      int wildcardId = 0;
      scenario.WithPlayer(1, player =>
          wildcardId = player.hand.freeTiles.Single(t => t.tile.IsWildcard).traceId);
      inquiry.ForPlayer(1, playerInquiry => playerInquiry
          .ApplyAction<TsumoAction>()
      ).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => {
        var info = ev.agariInfos.AssertTsumo(1).AssertYaku<Dora>(han: 1);
        // The wildcard completes 23s into 123s or 234s; the client is told which.
        var assignment = info.WildcardAssignments().Single();
        Assert.AreEqual(wildcardId, assignment.TraceId);
        CollectionAssert.Contains(new[] { (int)new Tile("1s").Val, new Tile("4s").Val },
            assignment.Tile);
      }).Resolve();
    }

    [TestMethod]
    public void NaturalWinsCarryNoWildcardAssignments() {
      var info = new AgariInfo(0, new ScoreStorage());
      Assert.AreEqual(0, info.WildcardAssignments().Count());
      Assert.AreEqual(0, info.ToProto([]).WildcardAssignments.Count);
    }
  }
}
