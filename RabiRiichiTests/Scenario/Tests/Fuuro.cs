using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Event.InGame;
using RabiRiichiTests.Helper;
using System.Threading.Tasks;

namespace RabiRiichiTests.Scenario.Tests {
    [TestClass]
    public class ScenarioFuuro {
        #region Chii
        [TestMethod]
        public async Task SimpleChii_CannotRiichi() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("78m11122357999p"))
                .WithWall(wall => wall.Reserve("9m"))
                .Start(1);

            (await scenario.WaitInquiry()).Finish();

            scenario.WithPlayer(2, player => Assert.IsTrue(player.hand.menzen));

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry
                .AssertSkip()
                .ChooseTiles<ChiiAction>("789m", action => {
                    Assert.AreEqual(1, action.options.Count);
                    return true;
                })
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            scenario.AssertNoEvent<DrawTileEvent>();

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                playerInquiry
                    .ChooseTile<PlayTileAction>("3p", action => {
                        action.options.ToTiles().AssertEquals("11122357999p");
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.WithPlayer(2, player => {
                Assert.IsFalse(player.hand.menzen);
            });

            (await scenario.WaitPlayerTurn(2)).ForPlayer(2, playerInquiry => {
                playerInquiry.AssertNoAction<RiichiAction>();
            });
        }
        #endregion
    }
}