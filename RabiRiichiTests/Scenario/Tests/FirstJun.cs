using Microsoft.VisualBasic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using System.Linq;
using System.Threading.Tasks;


namespace RabiRiichiTests.Scenario.Tests {
    [TestClass]
    public class ScenarioFirstJun {
        [TestMethod]
        public async Task DealerTenhou() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, (playerBuilder) => {
                    playerBuilder.SetFreeTiles("11123455678999s");
                })
                .SetFirstJun()
                .Start(0);

            (await scenario.WaitInquiry()).ForPlayer(0)
                .AssertAction<RiichiAction>()
                .AssertAction<PlayTileAction>()
                .ApplyAction<TsumoAction>()
                .AssertNoMoreActions()
                .AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => {
                Assert.AreEqual(0, ev.agariInfos.fromPlayer);
                Assert.IsTrue(ev.agariInfos.All(info => info.playerId == 0));
                Assert.IsTrue(ev.agariInfos.Any(info => (
                    info.scores.cachedResult.IsYakuman && info.scores.Any(score => score.Source is Tenhou)
                )));
                return true;
            }).Resolve();
        }
    }
}