using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using RabiRiichiTests.Helper;
using System.Threading.Tasks;

namespace RabiRiichiTests.Scenario.Tests {
    [TestClass]
    public class ScenarioDoraOption {
        #region Dora
        [TestMethod]
        public async Task SuccessDora() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("222s12345678p12z"))
                .WithWall(wall => wall
                    .Reserve("2s")
                    .AddDoras("12345s")
                    .AddUradoras("12345m"))
                .Start(2);

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry
                .ChooseTile<PlayTileAction>("2s")
            ).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .AssertSkip()
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertRon(2, 1)
                .AssertScore(han: 5)
                .AssertYaku<Tanyao>()
                .AssertYaku<SanshokuDoujun>()
                .AssertYaku<Dora>(han: 2)
                .AssertYaku<Akadora>(han: 1)
            ).AssertEvent<ConcludeGameEvent>(ev => {
                ev.doras.AssertEquals("12345s");
                ev.uradoras.AssertEquals("12345m");
            }).Resolve();
        }
        #endregion

    }
}