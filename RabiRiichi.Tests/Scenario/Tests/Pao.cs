using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Core.Config;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using System.Linq;
using System.Threading.Tasks;


namespace RabiRiichi.Tests.Scenario.Tests {
    [TestClass]
    public class ScenarioPao {
        #region Ron
        [TestMethod]
        public async Task SuccessPao_Ron() {
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
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(32000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-16000, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(-16000, ev.scoreChange.DeltaScore(0));
                Assert.IsTrue(ev.scoreChange.Any(sc => sc.reason == ScoreTransferReason.Pao), "No Pao transaction found.");
            })
            .Resolve();
        }

        [TestMethod]
        public async Task SuccessPao_RonMultiYaku() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("2z")
                    .AddCalled("5555z", 2, 2)
                    .AddCalled("6666z")
                    .AddCalled("7777z", 2, 3)
                    .AddCalled("1111z", 3, 0)
                )
                .WithWall(wall => wall.Reserve("2z"))
                .Start(2);

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry
                .ChooseTile<PlayTileAction>("2z")
            ).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .AssertSkip()
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => {
                ev.agariInfos
                    .AssertRon(2, 1)
                    .AssertScore(yakuman: 3)
                    .AssertYaku<Daisangen>()
                    .AssertYaku<Tsuuiisou>()
                    .AssertYaku<Suukantsu>();
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(96000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-16000, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(-64000, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(-16000, ev.scoreChange.DeltaScore(3));
                Assert.IsTrue(ev.scoreChange.Any(sc => sc.reason == ScoreTransferReason.Pao), "No Pao transaction found.");
            })
            .Resolve();
        }

        [TestMethod]
        public async Task SuccessPao_RonSamePlayer() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("2z")
                    .AddCalled("5555z", 2, 2)
                    .AddCalled("6666z")
                    .AddCalled("7777z", 2, 0)
                    .AddCalled("1111z", 3, 0)
                )
                .WithWall(wall => wall.Reserve("2z"))
                .Start(2);

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry
                .ChooseTile<PlayTileAction>("2z")
            ).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .AssertSkip()
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => {
                ev.agariInfos
                    .AssertRon(2, 1)
                    .AssertScore(yakuman: 3)
                    .AssertYaku<Daisangen>()
                    .AssertYaku<Tsuuiisou>()
                    .AssertYaku<Suukantsu>();
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(96000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-32000, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(-64000, ev.scoreChange.DeltaScore(2));
                Assert.IsTrue(ev.scoreChange.Any(sc => sc.reason == ScoreTransferReason.Pao), "No Pao transaction found.");
            })
            .Resolve();
        }
        #endregion

        #region Tsumo
        [TestMethod]
        public async Task SuccessPao_Tsumo() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("9s")
                    .AddCalled("111z", 0, 2)
                    .AddCalled("222z", 1, 2)
                    .AddCalled("333z", 2, 2)
                    .AddCalled("444z", 2, 0)
                )
                .WithWall(wall => wall.Reserve("9s"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .AssertAction<PlayTileAction>()
                .ApplyAction<TsumoAction>()
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => {
                ev.agariInfos
                    .AssertTsumo(1)
                    .AssertScore(yakuman: 2)
                    .AssertYaku<Daisuushii>();
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(64000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-64000, ev.scoreChange.DeltaScore(0));
            })
            .Resolve();
        }

        [TestMethod]
        public async Task SuccessPao_TsumoMultiYaku() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("2z")
                    .AddCalled("5555z", 2, 0)
                    .AddCalled("6666z")
                    .AddCalled("7777z", 2, 3)
                    .AddCalled("4444z", 3, 2)
                )
                .WithWall(wall => wall.Reserve("2z"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .AssertAction<PlayTileAction>()
                .ApplyAction<TsumoAction>()
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => {
                ev.agariInfos
                    .AssertTsumo(1)
                    .AssertScore(yakuman: 3)
                    .AssertYaku<Daisangen>()
                    .AssertYaku<Tsuuiisou>()
                    .AssertYaku<Suukantsu>();
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(96000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-16000, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(-40000, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(-40000, ev.scoreChange.DeltaScore(3));
                Assert.IsTrue(ev.scoreChange.Any(sc => sc.reason == ScoreTransferReason.Pao), "No Pao transaction found.");
            })
            .Resolve();
        }

        [TestMethod]
        public async Task SuccessPao_TsumoSamePlayer() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("2z")
                    .AddCalled("5555z", 2, 0)
                    .AddCalled("6666z")
                    .AddCalled("7777z", 2, 2)
                    .AddCalled("4444z", 3, 2)
                )
                .WithWall(wall => wall.Reserve("2z"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .AssertAction<PlayTileAction>()
                .ApplyAction<TsumoAction>()
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => {
                ev.agariInfos
                    .AssertTsumo(1)
                    .AssertScore(yakuman: 3)
                    .AssertYaku<Daisangen>()
                    .AssertYaku<Tsuuiisou>()
                    .AssertYaku<Suukantsu>();
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(96000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-16000, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(-72000, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(-8000, ev.scoreChange.DeltaScore(3));
                Assert.IsTrue(ev.scoreChange.Any(sc => sc.reason == ScoreTransferReason.Pao), "No Pao transaction found.");
            })
            .Resolve();
        }
        #endregion

        #region Failed
        [TestMethod]
        public async Task FailPao_RonButAnKan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("9s")
                    .AddCalled("2222s")
                    .AddCalled("3333p")
                    .AddCalled("7777z", 2, 0)
                    .AddCalled("8888s")
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
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(32000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-32000, ev.scoreChange.DeltaScore(2));
                Assert.IsFalse(ev.scoreChange.Any(sc => sc.reason == ScoreTransferReason.Pao), "Unexpected Pao transaction found.");
            })
            .Resolve();
        }

        [TestMethod]
        public async Task FailPao_RonButNotAllAreCalled() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("11s44z")
                    .AddCalled("111z", 0, 1)
                    .AddCalled("222z", 1, 1)
                    .AddCalled("333z", 2, 0)
                )
                .WithWall(wall => wall.Reserve("4z"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .AssertAction<PlayTileAction>()
                .ApplyAction<TsumoAction>()
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => {
                ev.agariInfos
                    .AssertTsumo(1)
                    .AssertScore(yakuman: 2)
                    .AssertYaku<Daisuushii>();
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(64000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-32000, ev.scoreChange.DeltaScore(0));
                Assert.AreEqual(-16000, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(-16000, ev.scoreChange.DeltaScore(3));
                Assert.IsFalse(ev.scoreChange.Any(sc => sc.reason == ScoreTransferReason.Pao), "Unexpected Pao transaction found.");
            })
            .Resolve();
        }

        [TestMethod]
        public async Task FailPao_ConfigOff() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("9s")
                    .AddCalled("2222s")
                    .AddCalled("3333p")
                    .AddCalled("7777z", 2, 2)
                    .AddCalled("8888s", 3, 0)
                )
                .WithWall(wall => wall.Reserve("9s"))
                .WithConfig(config => config
                    .SetAgariOption(AgariOption.Default & ~AgariOption.Pao))
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
            }).AssertEvent<ApplyScoreEvent>(ev => {
                Assert.AreEqual(32000, ev.scoreChange.DeltaScore(1));
                Assert.AreEqual(-32000, ev.scoreChange.DeltaScore(2));
                Assert.AreEqual(0, ev.scoreChange.DeltaScore(0));
                Assert.IsTrue(ev.scoreChange.All(sc => sc.reason != ScoreTransferReason.Pao), "Unexpected pao transaction found.");
            })
            .Resolve();
        }
        #endregion
    }
}