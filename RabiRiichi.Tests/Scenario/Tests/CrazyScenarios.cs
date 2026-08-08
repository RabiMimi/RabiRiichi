using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events.InGame;
using RabiRiichi.Patterns;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Tests {
  [TestClass]
  public class CrazyScenarios {
    #region Tsumo
    [TestMethod]
    public async Task 天地创造改一() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config.SetScoringOption(ScoringOption.Aotenjou))
          .WithState(state => state.SetRound(Wind.E, 0, 1).SetRiichiStick(1))
          .WithPlayer(1, playerBuilder => playerBuilder
              .SetFreeTiles("5z")
              .AddCalled("1111z")
              .AddCalled("2222z")
              .AddCalled("3333z", 0, 0)
              .AddCalled("4444z"))
          .WithWall(wall => wall.Reserve("5z").AddDoras("1m"))
          .Build(1)
          .WithPlayer(1, player => {
            foreach (var tile in player.hand.called.SelectMany(m => m)) {
              tile.tile = new Tile("5z");
            }
          })
          .Start();

      (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
          .ApplyAction<TsumoAction>()
      ).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertTsumo(1)
          .AssertScore(32, 140)
          .AssertYaku<Sanankou>(han: 2)
          .AssertYaku<YakuhaiHaku>(han: 4)
          .AssertYaku<Suukantsu>()
          .AssertYaku<Tsuuiisou>()
      ).AssertEvent<ApplyScoreEvent>(ev => {
        Assert.AreEqual(-4810363371700L, ev.scoreChange.DeltaScore(0));
        Assert.AreEqual(9620726744500L, ev.scoreChange.DeltaScore(1));
        Assert.AreEqual(-2405181685900L, ev.scoreChange.DeltaScore(2));
        Assert.AreEqual(-2405181685900L, ev.scoreChange.DeltaScore(3));
      }).Resolve();
    }

    [TestMethod]
    public async Task 天地创造() {
      var all5z = new Tiles(System.Linq.Enumerable.Repeat(new Tile("5z"), 136));
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config
              .SetScoringOption(ScoringOption.Aotenjou)
              .SetInitialTiles(all5z))
          .WithState(state => state.SetRound(Wind.E, 0, 0))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("55555555555555z"))
          .SetFirstJun()
          .Start(0);

      var firstInquiry = await scenario.WaitInquiry();
      firstInquiry.ForPlayer(0, playerInquiry => playerInquiry
          .AssertAction<TsumoAction>(tenhou => scenario.WithPlayer(0, player =>
              Assert.AreSame(tenhou.incoming, player.hand.pendingTile,
                  "The tile scored as the Tenhou win must also be the pending draw.")))
          .ChooseTiles<KanAction>("5555z"))
          .AssertAutoFinish();

      // Declining Tenhou in favor of ankan must leave a valid 13-tile hand for
      // the replacement draw's resolvers.
      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry =>
          playerInquiry.AssertAction<PlayTileAction>());
    }
    #endregion
  }
}
