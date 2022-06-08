using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Tests {
    [TestClass]
    public class ScenarioFuriten {
        #region Temporary Furiten
        [TestMethod]
        public async Task TemporaryFuriten() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, player => {
                    player.SetFreeTiles("123456789m11z34s");
                })
                .WithWall(wall => {
                    wall.Reserve("5m2s2s2s5m");
                })
                .Start(1);

            (await scenario.WaitInquiry()).Finish(); // 玩家1摸切

            scenario.WithPlayer(1, player => {
                Assert.IsFalse(player.hand.isFuriten);
                Assert.IsFalse(player.hand.isTempFuriten);
            });

            (await scenario.WaitInquiry()).Finish(); // 玩家2摸切

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ApplySkip();
            }).AssertAutoFinish();  // 玩家1见逃

            var inquiry = await scenario.WaitInquiry(); // 玩家3回合

            // Enters Temporary Furiten after refusing to Ron a valid discarded tile
            scenario.WithPlayer(1, player => {
                Assert.IsTrue(player.hand.isFuriten);
                Assert.IsTrue(player.hand.isTempFuriten);
            });

            inquiry.Finish(); // 玩家3摸切

            // Cannot Ron when furiten
            (await scenario.WaitInquiry()).AssertNoActionForPlayer(1).Finish(); // 玩家0摸切

            // Exits Temporary Furiten after drawing a regular tile
            (await scenario.WaitInquiry()).Finish(); // 玩家1摸切

            await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                Assert.IsFalse(player.hand.isFuriten);
                Assert.IsFalse(player.hand.isTempFuriten);
            });
        }

        [TestMethod]
        public async Task TemporaryFuriten_Chankan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, player => player
                    .SetFreeTiles("333567m2344p")
                    .AddCalled("222m", 0, 0))
                .WithPlayer(2, player => player
                    .SetFreeTiles("123789m1234z")
                    .AddCalled("111p", 0, 0))
                .WithWall(wall => {
                    wall.Reserve("1p1z1z4p").AddDoras("12z");
                })
                .Start(2);

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                playerInquiry.ChooseTiles<KanAction>("1111p");
            }).AssertAutoFinish(); // Kan

            scenario.WithPlayer(1, player => {
                Assert.IsFalse(player.hand.isFuriten);
                Assert.IsFalse(player.hand.isTempFuriten);
            });

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ApplySkip();
            }).AssertAutoFinish();

            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                Assert.IsTrue(player.hand.isFuriten);
                Assert.IsTrue(player.hand.isTempFuriten);
            });

            inquiry.Finish();

            (await scenario.WaitPlayerTurn(1)).ForPlayer(1, playerInquiry => {
                playerInquiry.ApplyAction<TsumoAction>();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertTsumo(1)
                .AssertScore(han: 1)
                .AssertYaku<Tanyao>()).Resolve();
        }
        #endregion
    }
}