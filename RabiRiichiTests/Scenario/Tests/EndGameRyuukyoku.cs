using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Event.InGame;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichiTests.Scenario.Tests {
    [TestClass]
    public class ScenarioEndGameRyuukyoku {
        #region Tenpai
        [TestMethod]
        public async Task Ryuukyoku_NoTen() {
            var scenario = new ScenarioBuilder()
                .SetRound(Wind.E, Wind.N, 1)
                .WithPlayer(0, playerBuilder => playerBuilder
                    .SetFreeTiles("111234567899m7z")
                    .SetDiscarded(10, "3s"))
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("111234567899p7z")
                    .SetDiscarded(10, "4s"))
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("111234567899s7z")
                    .SetDiscarded(10, "5s"))
                .WithPlayer(3, playerBuilder => playerBuilder
                    .SetFreeTiles("1112223334457z")
                    .SetDiscarded(10, "6s"))
                .WithWall(wall => wall.Reserve("6z"))
                .Build(0)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).Finish();

            await scenario.AssertEvent<EndGameRyuukyokuEvent>(ev => {
                Assert.IsTrue(ev.nagashiManganPlayers.Length == 0);
                Assert.IsTrue(ev.tenpaiPlayers.Length == 0);
                Assert.IsTrue(ev.remainingPlayers.SequenceEqualAfterSort(
                    Enumerable.Range(0, 4)));
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(0, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(0, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(0, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(0, ev.scoreChange.DeltaScore(3));
            }).AssertEvent<BeginGameEvent>(ev => {
                Assert.AreEqual(1, ev.round);
                Assert.AreEqual(0, ev.dealer);
                Assert.AreEqual(2, ev.honba);
            })
            .Resolve();
        }
        #endregion
    }
}