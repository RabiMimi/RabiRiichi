using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichiTests.Scenario.Tests {
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
        #endregion
    }
}