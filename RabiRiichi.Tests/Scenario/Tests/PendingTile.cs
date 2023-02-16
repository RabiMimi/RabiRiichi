using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Tests.Helper;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Tests {
    [TestClass]
    public class ScenarioPendingTile {
        [TestMethod]
        public async Task PendingTile_Success() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("78m11122357999p"))
                .WithWall(wall => wall.Reserve("9m"))
                .Start(1);

            // Wait for player 1
            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(2, player => {
                Assert.IsNull(player.hand.pendingTile);
            });

            scenario.WithPlayer(1, player => {
                player.hand.pendingTile.tile.AssertEquals("9m");
            });

            inquiry.Finish();

            // Wait for player 2 to Chii
            inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                Assert.IsNull(player.hand.pendingTile);
            });

            inquiry.ForPlayer(2, playerInquiry => playerInquiry
                .AssertSkip()
                .ChooseTiles<ChiiAction>("789m")
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            scenario.WithPlayer(2, player => {
                Assert.IsNull(player.hand.pendingTile);
            });

            // Wait for player 2 to play a tile
            inquiry = await scenario.WaitInquiry();

            inquiry.ForPlayer(2, playerInquiry => {
                scenario.WithGameInfo(info => Assert.AreEqual(2, info.currentPlayer));
                playerInquiry
                    .ChooseTile<PlayTileAction>("3p")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.WaitInquiry();

            scenario.WithPlayer(2, player => {
                Assert.IsNull(player.hand.pendingTile);
            });
        }

        [TestMethod]
        public async Task PendingTile_Kan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("78m11122357999p"))
                .WithWall(wall => wall.Reserve("9p").AddRinshan("12p"))
                .Start(1);

            // Wait for player 1 to Kan
            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                player.hand.pendingTile.tile.AssertEquals("9p");
            });

            inquiry.ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTiles<KanAction>("9999p");
            }).AssertAutoFinish();

            // Wait for player 1 to Kan again
            inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                player.hand.pendingTile.tile.AssertEquals("1p");
            });

            inquiry.ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTiles<KanAction>("1111p");
            }).AssertAutoFinish();

            // Wait for player 1 to play a tile
            inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                player.hand.pendingTile.tile.AssertEquals("2p");
            });

            inquiry.Finish();

            // Wait for player 2 to take action
            await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                Assert.IsNull(player.hand.pendingTile);
            });
        }

        [TestMethod]
        public async Task PendingTile_ClearsBetweenRounds() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("67788p12355566m"))
                .WithWall(wall => wall.Reserve("9p"))
                .WithState(state => state
                    .SetRound(Wind.E, 2))
                .Start(1);

            // Wait for player 1 to Tsumo
            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                player.hand.pendingTile.tile.AssertEquals("9p");
            });

            inquiry.ForPlayer(1, playerInquiry => {
                playerInquiry.ApplyAction<TsumoAction>();
            }).AssertAutoFinish();

            // Wait for next game to start
            inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                Assert.IsNull(player.hand.pendingTile);
            });
        }
    }
}