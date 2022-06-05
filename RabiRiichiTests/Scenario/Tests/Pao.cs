using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Core.Config;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using System.Threading.Tasks;


namespace RabiRiichiTests.Scenario.Tests {
    [TestClass]
    public class ScenarioPao {
        #region Single Pao
        [TestMethod]
        public async Task SuccessPao_DiffPlayer() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("9s")
                    .AddCalled("2222s")
                    .AddCalled("3333p")
                    .AddCalled("7777z", 2, 2)
                    .AddCalled("8888s", 3, 0)
                )
                .WithWall(wall => wall.Reserve("9s"))
                .Start(2);

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry
                .ChooseTile<PlayTileAction>("9s")
            ).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .AssertSkip()
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => {
                ev.agariInfos
                    .AssertRon(2, 1)
                    .AssertScore(yakuman: 1)
                    .AssertYaku<Suukantsu>();
                return true;
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(32000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-16000, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(-16000, ev.scoreChange.DeltaScore(0));
                return true;
            })
            .Resolve();
        }
        #endregion

        #region Multiple Pao
        #endregion
    }
}