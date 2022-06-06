using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Event.InGame;
using RabiRiichiTests.Helper;
using System.Linq;
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
                Assert.IsTrue(player.hand.called.ToStrings().SequenceEqual(new string[] { "789m" }));
            });

            (await scenario.WaitPlayerTurn(2)).ForPlayer(2, playerInquiry => {
                playerInquiry.AssertNoAction<RiichiAction>();
            });
        }

        [TestMethod]
        public async Task Chii_MultiChoice() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("34r55678m227999p"))
                .WithWall(wall => wall.Reserve("6m"))
                .Start(1);

            (await scenario.WaitInquiry()).Finish();

            scenario.WithPlayer(2, player => Assert.IsTrue(player.hand.menzen));

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry
                .AssertSkip()
                .ChooseTiles<ChiiAction>("4r56m", action => {
                    action.options.ToStrings().SequenceEqualAfterSort(
                        new string[] { "456m", "4r56m", "567m", "r567m", "678m" }
                    );
                    return true;
                })
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            scenario.AssertNoEvent<DrawTileEvent>();

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>(action => {
                        action.options.ToTiles().AssertEquals("578m227999p");
                        return true;
                    })
                    .AssertNoMoreActions();
            });
        }
        #endregion
    }
}