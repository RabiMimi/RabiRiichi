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
    }
}