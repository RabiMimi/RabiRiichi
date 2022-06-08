using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Tests {
    [TestClass]
    public class ScenarioFuriten {
        [TestMethod]
        public async Task GeneralFuritenTest() {
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

            // TODO: https://github.com/RabiMimi/RabiRiichi/issues/18
        }
    }
}