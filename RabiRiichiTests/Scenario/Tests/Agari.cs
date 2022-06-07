using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Core;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using System.Threading.Tasks;

namespace RabiRiichiTests.Scenario.Tests {
    [TestClass]
    public class ScenarioAgari {
        #region Tsumo
        [TestMethod]
        public async Task DealerTsumo() {
            var scenario = new ScenarioBuilder()
                .WithState(state => state.SetRound(Wind.E, 0, 2).SetRiichiStick(2))
                .WithPlayer(0, playerBuilder => playerBuilder
                    .SetFreeTiles("123m123p1123s")
                    .AddCalled("111m", 0, 2))
                .WithWall(wall => wall.Reserve("1s").AddDoras("2m"))
                .Start(0);

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => playerInquiry
                .ApplyAction<TsumoAction>()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertTsumo(0)
                .AssertScore(4, 30)
                .AssertYaku<SanshokuDoujun>(han: 1)
                .AssertYaku<JunchanTaiyao>(han: 2)
                .AssertYaku<Dora>(han: 1)
            ).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(14300, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(-4100, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-4100, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(-4100, ev.scoreChange.DeltaScore(3));
            }).AssertEvent<BeginGameEvent>(ev => {
                Assert.AreEqual(0, ev.round);
                Assert.AreEqual(0, ev.dealer);
                Assert.AreEqual(3, ev.honba);
            })
            .Resolve();

            scenario.WithGame(game => {
                Assert.AreEqual(0, game.info.round);
                Assert.AreEqual(0, game.info.dealer);
                Assert.AreEqual(3, game.info.honba);
                Assert.AreEqual(0, game.info.riichiStick);
            });
        }
        #endregion
    }
}