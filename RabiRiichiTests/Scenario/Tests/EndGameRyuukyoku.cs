using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Event.InGame;
using System;
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

        public static async Task<Scenario> Build1Ten(int dealer, Action<ScenarioBuilder> action = null) {
            var scenarioBuilder = new ScenarioBuilder()
                .WithState(state => state
                    .SetRound(Wind.S, dealer, 3)
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
                .WithWall(wall => wall.Reserve("6z"));
            action?.Invoke(scenarioBuilder);
            var scenario = scenarioBuilder
                .Build(0)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).Finish();

            scenario.AssertEvent<EndGameRyuukyokuEvent>(ev => {
                Assert.IsTrue(ev.nagashiManganPlayers.Length == 0);
                Assert.IsTrue(ev.tenpaiPlayers.SequenceEqualAfterSort(0));
                Assert.IsTrue(ev.remainingPlayers
                    .SequenceEqualAfterSort(0, 1, 2, 3));
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(3000, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(-1000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-1000, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(-1000, ev.scoreChange.DeltaScore(3));
            });

            return scenario;
        }

        [TestMethod]
        public async Task Ryuukyoku_1Ten() {
            await (await Build1Ten(0))
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.AreEqual(1, ev.round);
                    Assert.AreEqual(0, ev.dealer);
                    Assert.AreEqual(4, ev.honba);
                    Assert.AreEqual(3, ev.game.info.riichiStick);
                })
                .Resolve();
        }

        [TestMethod]
        public async Task Ryuukyoku_NoRenchan() {
            await (await Build1Ten(0, scenarioBuilder => {
                scenarioBuilder.WithConfig(config => config.SetRenchanPolicy(
                    RenchanPolicy.Default & ~RenchanPolicy.DealerTenpai
                ));
            })).AssertEvent<BeginGameEvent>(ev => {
                Assert.AreEqual(1, ev.round);
                Assert.AreEqual(1, ev.dealer);
                Assert.AreEqual(4, ev.honba);
                Assert.AreEqual(3, ev.game.info.riichiStick);
            }).Resolve();
        }

        [TestMethod]
        public async Task Ryuukyoku_AlwaysRenchan() {
            await (await Build1Ten(1, scenarioBuilder => {
                scenarioBuilder.WithConfig(config => config.SetRenchanPolicy(
                    RenchanPolicy.Default | RenchanPolicy.Ryuukyoku
                ));
            })).AssertEvent<BeginGameEvent>(ev => {
                Assert.AreEqual(1, ev.round);
                Assert.AreEqual(1, ev.dealer);
                Assert.AreEqual(4, ev.honba);
                Assert.AreEqual(3, ev.game.info.riichiStick);
            }).Resolve();
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
                    .SetFreeTiles("2345678999m")
                    .AddCalled("111m", 0, 1)
                    .SetDiscarded(10, "3s"))
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("1115678999p")
                    .AddCalled("234p", 0, 0)
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

        #region NagashiMangan
        [TestMethod]
        public async Task SuccessNagashiMangan_Menzen() {
            var scenario = new ScenarioBuilder()
                .WithState(state => state
                    .SetRound(Wind.E, 0, 0))
                .WithPlayer(0, playerBuilder => playerBuilder
                    .SetDiscarded(16, "19s19p19m1234567777z"))
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetDiscarded(1, "5s"))
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetDiscarded(1, "6s"))
                .WithPlayer(3, playerBuilder => playerBuilder
                    .SetDiscarded(1, "7s"))
                .WithWall(wall => wall.Reserve("6z"))
                .Build(0)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).Finish();

            await scenario.AssertRyuukyoku<EndGameRyuukyokuEvent>(ev => {
                Assert.IsTrue(ev.nagashiManganPlayers.SequenceEqualAfterSort(0));
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(12000, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(-4000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-4000, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(-4000, ev.scoreChange.DeltaScore(3));
            }).AssertEvent<BeginGameEvent>(ev => {
                Assert.AreEqual(0, ev.round);
                Assert.AreEqual(1, ev.dealer);
                Assert.AreEqual(1, ev.honba);
            }).Resolve();
        }

        [TestMethod]
        public async Task SuccessNagashiMangan_Fuuro() {
            var scenario = new ScenarioBuilder()
                .WithState(state => state
                    .SetRound(Wind.E, 1, 1))
                .WithPlayer(0, playerBuilder => playerBuilder
                    .SetDiscarded(16, "19s19p19m1234567777z"))
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("2678p123m")
                    .SetDiscarded(2, "19s")
                    .AddCalled("3333p")
                    .AddCalled("444m", 1, 2))
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetDiscarded(1, "6s"))
                .WithPlayer(3, playerBuilder => playerBuilder
                    .SetDiscarded(1, "7s"))
                .WithWall(wall => wall.Reserve("6z"))
                .Build(2)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).Finish();

            await scenario.AssertRyuukyoku<EndGameRyuukyokuEvent>(ev => {
                Assert.IsTrue(ev.nagashiManganPlayers.SequenceEqualAfterSort(0, 1));
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(4000, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(8000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-6000, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(-6000, ev.scoreChange.DeltaScore(3));
            }).AssertEvent<BeginGameEvent>(ev => {
                Assert.AreEqual(0, ev.round);
                Assert.AreEqual(1, ev.dealer);
                Assert.AreEqual(2, ev.honba);
            }).Resolve();
        }

        [TestMethod]
        public async Task SuccessNagashiMangan_Everyone() {
            var scenario = new ScenarioBuilder()
                .WithState(state => state
                    .SetRound(Wind.E, 2, 1))
                .WithPlayer(0, playerBuilder => playerBuilder
                    .SetDiscarded(16, "19s19p19m1234567777z"))
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("2678p123444m")
                    .SetDiscarded(2, "19s")
                    .AddCalled("3333p"))
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetDiscarded(1, "9s"))
                .WithPlayer(3, playerBuilder => playerBuilder
                    .SetDiscarded(1, "1s"))
                .WithWall(wall => wall.Reserve("6z"))
                .Build(2)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).Finish();

            await scenario.AssertRyuukyoku<EndGameRyuukyokuEvent>(ev => {
                Assert.IsTrue(ev.nagashiManganPlayers.SequenceEqualAfterSort(0, 1, 2, 3));
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(0, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(0, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(0, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(0, ev.scoreChange.DeltaScore(3));
            }).AssertEvent<BeginGameEvent>(ev => {
                Assert.AreEqual(0, ev.round);
                Assert.AreEqual(3, ev.dealer);
                Assert.AreEqual(2, ev.honba);
            }).Resolve();
        }

        [TestMethod]
        public async Task NoNagashiMangan_DiscardedTileClaimed() {
            var scenario = new ScenarioBuilder()
                .WithState(state => state
                    .SetRound(Wind.E, 2, 1))
                .WithPlayer(0, playerBuilder => playerBuilder
                    .SetDiscarded(16, "19s19p19m1234567777z"))
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("2678p123m")
                    .SetDiscarded(2, "23s")
                    .AddCalled("111z", 0, 0)
                    .AddCalled("3333p"))
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetDiscarded(1, "4s"))
                .WithPlayer(3, playerBuilder => playerBuilder
                    .SetDiscarded(1, "5s"))
                .WithWall(wall => wall.Reserve("6z"))
                .Build(2)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).Finish();

            await scenario.AssertRyuukyoku<EndGameRyuukyokuEvent>(ev => {
                Assert.AreEqual(0, ev.nagashiManganPlayers.Length);
                Assert.IsTrue(ev.tenpaiPlayers.SequenceEqualAfterSort(1));
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(-1000, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(3000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-1000, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(-1000, ev.scoreChange.DeltaScore(3));
            }).AssertEvent<BeginGameEvent>(ev => {
                Assert.AreEqual(0, ev.round);
                Assert.AreEqual(3, ev.dealer);
                Assert.AreEqual(2, ev.honba);
            }).Resolve();
        }

        #endregion
    }
}