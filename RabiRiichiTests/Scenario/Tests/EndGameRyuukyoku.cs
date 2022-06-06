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
                .WithState(state => state.SetRound(Wind.E, 3, 1))
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
                Assert.IsTrue(ev.remainingPlayers
                    .SequenceEqualAfterSort(0, 1, 2, 3));
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

        [TestMethod]
        public async Task Ryuukyoku_1Ten() {
            var scenario = new ScenarioBuilder()
                .WithState(state => state
                    .SetRound(Wind.S, 0, 3)
                    .SetRiichiStick(3))
                .WithPlayer(0, playerBuilder => playerBuilder
                    .SetFreeTiles("1112345678999m")
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
                Assert.IsTrue(ev.tenpaiPlayers.SequenceEqualAfterSort(0));
                Assert.IsTrue(ev.remainingPlayers
                    .SequenceEqualAfterSort(0, 1, 2, 3));
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(3000, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(-1000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-1000, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(-1000, ev.scoreChange.DeltaScore(3));
            }).AssertEvent<BeginGameEvent>(ev => {
                Assert.AreEqual(1, ev.round);
                Assert.AreEqual(0, ev.dealer);
                Assert.AreEqual(4, ev.honba);
                Assert.AreEqual(3, ev.game.info.riichiStick);
            })
            .Resolve();
        }


        [TestMethod]
        public async Task Ryuukyoku_2Ten() {
            var scenario = new ScenarioBuilder()
                .WithState(state => state.SetRound(Wind.S, 2, 3))
                .WithPlayer(0, playerBuilder => playerBuilder
                    .SetFreeTiles("1112345678999m")
                    .SetDiscarded(10, "3s"))
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("1112345678999p")
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
                Assert.IsTrue(ev.tenpaiPlayers.SequenceEqualAfterSort(0, 1));
                Assert.IsTrue(ev.remainingPlayers
                    .SequenceEqualAfterSort(0, 1, 2, 3));
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(1500, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(1500, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-1500, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(-1500, ev.scoreChange.DeltaScore(3));
            }).AssertEvent<BeginGameEvent>(ev => {
                Assert.AreEqual(1, ev.round);
                Assert.AreEqual(3, ev.dealer);
                Assert.AreEqual(4, ev.honba);
            })
            .Resolve();
        }

        [TestMethod]
        public async Task Ryuukyoku_3Ten() {
            var scenario = new ScenarioBuilder()
                .WithState(state => state
                    .SetRound(Wind.E, 0, 0))
                .WithPlayer(0, playerBuilder => playerBuilder
                    .SetFreeTiles("1112345678999m")
                    .SetDiscarded(10, "3s"))
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("1112345678999p")
                    .SetDiscarded(10, "4s"))
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("1112345678999s")
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
                Assert.IsTrue(ev.tenpaiPlayers.SequenceEqualAfterSort(0, 1, 2));
                Assert.IsTrue(ev.remainingPlayers
                    .SequenceEqualAfterSort(0, 1, 2, 3));
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(1000, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(1000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(1000, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(-3000, ev.scoreChange.DeltaScore(3));
            }).AssertEvent<BeginGameEvent>(ev => {
                Assert.AreEqual(0, ev.round);
                Assert.AreEqual(0, ev.dealer);
                Assert.AreEqual(1, ev.honba);
            })
            .Resolve();
        }

        [TestMethod]
        public async Task Ryuukyoku_AllTen() {
            var scenario = new ScenarioBuilder()
                .WithState(state => state
                    .SetRound(Wind.E, 0, 0))
                .WithPlayer(0, playerBuilder => playerBuilder
                    .SetFreeTiles("1112345678999m")
                    .SetDiscarded(10, "3s"))
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("1112345678999p")
                    .SetDiscarded(10, "4s"))
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("1112345678999s")
                    .SetDiscarded(10, "5s"))
                .WithPlayer(3, playerBuilder => playerBuilder
                    .SetFreeTiles("1112223334445z")
                    .SetDiscarded(10, "6s"))
                .WithWall(wall => wall.Reserve("6z"))
                .Build(0)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).Finish();

            await scenario.AssertEvent<EndGameRyuukyokuEvent>(ev => {
                Assert.IsTrue(ev.nagashiManganPlayers.Length == 0);
                Assert.IsTrue(ev.tenpaiPlayers
                    .SequenceEqualAfterSort(0, 1, 2, 3));
                Assert.IsTrue(ev.remainingPlayers
                    .SequenceEqualAfterSort(0, 1, 2, 3));
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(0, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(0, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(0, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(0, ev.scoreChange.DeltaScore(3));
            }).AssertEvent<BeginGameEvent>(ev => {
                Assert.AreEqual(0, ev.round);
                Assert.AreEqual(0, ev.dealer);
                Assert.AreEqual(1, ev.honba);
            })
            .Resolve();
        }
        #endregion
    }
}