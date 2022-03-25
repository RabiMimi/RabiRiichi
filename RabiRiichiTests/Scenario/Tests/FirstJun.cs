using Microsoft.VisualBasic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
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

            (await scenario.WaitInquiry()).ForPlayer(0, (playerInquiry) => {
                playerInquiry
                    .AssertAction<RiichiAction>()
                    .AssertAction<PlayTileAction>()
                    .ApplyAction<TsumoAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => {
                ev.agariInfos
                    .AssertTsumo(0)
                    .AssertScore(1, 40, 3)
                    .AssertYaku<Tenhou>(yakuman: 1)
                    .AssertYaku<JunseiChuurenPoutou>(yakuman: 2);
                return true;
            }).Resolve();
        }

        [TestMethod]
        public async Task NonDealerChiihou() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, (playerBuilder) => {
                    playerBuilder.SetFreeTiles("149p258s369m12347z");
                })
                .WithPlayer(1, (playerBuilder) => {
                    playerBuilder.SetFreeTiles("1112345678999s");
                })
                .WithWall(wallBuilder => {
                    wallBuilder.Reserve("2345678s");
                })
                .SetFirstJun()
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<RiichiAction>()
                    .AssertAction<PlayTileAction>()
                    .ApplyAction<TsumoAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => {
                ev.agariInfos
                    .AssertTsumo(1)
                    .AssertScore(1, 40, 3)
                    .AssertYaku<Chiihou>(yakuman: 1)
                    .AssertYaku<JunseiChuurenPoutou>(yakuman: 2);
                return true;
            }).Resolve();

        }
    }
}