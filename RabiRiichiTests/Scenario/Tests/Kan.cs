using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Core;
using RabiRiichi.Event.InGame;
using System.Threading.Tasks;


namespace RabiRiichiTests.Scenario.Tests {
    [TestClass]
    public class ScenarioKan {
        [TestMethod]
        public async Task SuccessDaiMinKan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("11112223333s19m");
                })
                .WithWall(wall => wall.Reserve("2s"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("2s");
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .AssertAction<PonAction>()
                    .ChooseTiles<KanAction>("2222s", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            Kan kan = null;
            await scenario.AssertEvent<AddKanEvent>((ev) => {
                kan = ev.kan;
                Assert.AreEqual(TileSource.DaiMinKan, ev.kanSource);
                return true;
            }).Resolve();

            scenario.WithGame(game => {
                CollectionAssert.Contains(game.GetPlayer(0).hand.called, kan);
            });
        }
    }
}