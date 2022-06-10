using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events.InGame;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Helper;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Tests {
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
                })
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            scenario.AssertNoEvent<DrawTileEvent>();

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                scenario.WithGameInfo(info => Assert.AreEqual(2, info.currentPlayer));
                playerInquiry
                    .ChooseTile<PlayTileAction>("3p", action => {
                        action.options.ToTiles().AssertEquals("11122357999p");
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

        private static async Task<Scenario> MultiChoiceChiiScenario(string expectedDiscardTiles, Action<ScenarioBuilder> setup = null) {
            var scenarioBuilder = new ScenarioBuilder()
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("34r55678m227999p"))
                .WithWall(wall => wall.Reserve("6m"));
            setup?.Invoke(scenarioBuilder);
            var scenario = scenarioBuilder.Start(1);

            (await scenario.WaitInquiry()).Finish();

            scenario.WithPlayer(2, player => Assert.IsTrue(player.hand.menzen));

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry
                .AssertSkip()
                .ChooseTiles<ChiiAction>("4r56m", action => {
                    action.options.ToStrings().SequenceEqualAfterSort(
                        new string[] { "456m", "4r56m", "567m", "r567m", "678m" }
                    );
                })
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            scenario.AssertNoEvent<DrawTileEvent>();

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>(action => {
                        action.options.ToTiles().AssertEquals(expectedDiscardTiles);
                    })
                    .AssertNoMoreActions();
            });

            return scenario;
        }

        [TestMethod]
        public Task Chii_MultiChoice() {
            return MultiChoiceChiiScenario("578m227999p");
        }

        [TestMethod]
        public Task Chii_NoSujiKuikae() {
            return MultiChoiceChiiScenario("3578m227999p", scenarioBuilder => {
                scenarioBuilder.WithConfig(config =>
                    config.SetKuikaePolicy(KuikaePolicy.Genbutsu));
            });
        }

        [TestMethod]
        public Task Chii_NoGenbutsuKuikae() {
            return MultiChoiceChiiScenario("5678m227999p", scenarioBuilder => {
                scenarioBuilder.WithConfig(config =>
                    config.SetKuikaePolicy(KuikaePolicy.Suji));
            });
        }

        [TestMethod]
        public async Task FailChii_NotDiscardedByPrev() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("78m11122357999p"))
                .WithWall(wall => wall.Reserve("9m"))
                .Start(3);

            (await scenario.WaitInquiry()).Finish();

            (await scenario.WaitInquiry()).AssertNoActionForPlayer(2);
        }

        [TestMethod]
        public async Task FailChii_NoTileToPlay() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("4567m")
                    .AddCalled("111m", 0, 3)
                    .AddCalled("999m", 0, 3)
                    .AddCalled("111p", 0, 3))
                .WithWall(wall => wall.Reserve("4m"))
                .Start(1);

            (await scenario.WaitInquiry()).Finish();

            // No action for player 2
            await scenario.AssertEvent<NextPlayerEvent>().Resolve();
        }
        #endregion

        #region Pon
        [TestMethod]
        public async Task SimplePon_CannotRiichi() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("99m11122357999p"))
                .WithWall(wall => wall.Reserve("9m"))
                .Start(3);

            (await scenario.WaitInquiry()).Finish();

            scenario.WithPlayer(2, player => Assert.IsTrue(player.hand.menzen));

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry
                .AssertSkip()
                .ChooseTiles<PonAction>("999m", action => {
                    Assert.AreEqual(1, action.options.Count);
                })
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            scenario.AssertNoEvent<DrawTileEvent>();

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                playerInquiry
                    .ChooseTile<PlayTileAction>("3p", action => {
                        action.options.ToTiles().AssertEquals("11122357999p");
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.WithPlayer(2, player => {
                Assert.IsFalse(player.hand.menzen);
                Assert.IsTrue(player.hand.called.ToStrings().SequenceEqual(new string[] { "999m" }));
            }).AssertEvent<DrawTileEvent>(ev => {
                Assert.AreEqual(3, ev.playerId);
            }).Resolve();

            (await scenario.WaitPlayerTurn(2)).ForPlayer(2, playerInquiry => {
                playerInquiry.AssertNoAction<RiichiAction>();
            });
        }

        private static async Task<Scenario> MultiChoicePonScenario(string expectedDiscardTiles, Action<ScenarioBuilder> setup = null) {
            var scenarioBuilder = new ScenarioBuilder()
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("555r5678m227999p"));
            setup?.Invoke(scenarioBuilder);
            var scenario = scenarioBuilder.Build(0);
            scenario.WithWall(wall => wall.remaining[^1].tile = new Tile("5m")).Start();

            (await scenario.WaitInquiry()).Finish();

            scenario.WithPlayer(2, player => Assert.IsTrue(player.hand.menzen));

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry
                .AssertSkip()
                .AssertAction<KanAction>()
                .ChooseTiles<PonAction>("555m", action => {
                    action.options.ToStrings().SequenceEqualAfterSort(
                        new string[] { "555m", "55r5m" }
                    );
                })
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            scenario.AssertNoEvent<DrawTileEvent>();

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>(action => {
                        action.options.ToTiles().AssertEquals(expectedDiscardTiles);
                    })
                    .AssertNoMoreActions();
            });

            return scenario;
        }

        [TestMethod]
        public Task Pon_MultiChoice() {
            return MultiChoicePonScenario("678m227999p");
        }

        [TestMethod]
        public Task Pon_NoGenbutsuKuikae() {
            return MultiChoicePonScenario("5r5678m227999p", scenarioBuilder => {
                scenarioBuilder.WithConfig(config =>
                    config.SetKuikaePolicy(KuikaePolicy.Suji));
            });
        }

        [TestMethod]
        public async Task FailPon_DiscardBySelf() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("99m11122357999p"))
                .WithWall(wall => wall.Reserve("9m"))
                .Start(2);

            (await scenario.WaitInquiry()).Finish();

            (await scenario.WaitInquiry()).AssertNoActionForPlayer(2);
        }

        [TestMethod]
        public async Task FailPon_NoTileToPlay() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("555r5m")
                    .AddCalled("111m", 0, 3)
                    .AddCalled("999m", 0, 3)
                    .AddCalled("111p", 0, 3))
                .WithWall(wall => wall.AddDoras("1z"))
                .Build(0)
                .WithWall(wall => wall.remaining[^1].tile = new Tile("5m"))
                .Start();

            (await scenario.WaitInquiry()).Finish();

            // Player 2 cannot pon
            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ApplyAction<RonAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertRon(0, 2)
                .AssertScore(han: 3)
                .AssertYaku<Toitoi>()
                .AssertYaku<Akadora>(han: 1)
            ).Resolve();
        }
        #endregion

        #region ShiiaruRaotai
        [TestMethod]
        public async Task SuccessShiiaruRaotai() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("9m")
                    .AddCalled("123m", 0, 1)
                    .AddCalled("555s", 1, 3)
                    .AddCalled("456p", 1, 1)
                    .AddCalled("2222p"))
                .WithWall(wall => wall.Reserve("9m"))
                .WithConfig(config => config.Setup(setup => {
                    setup.AddExtraStdPattern<ShiiaruRaotai>();
                }))
                .Start(3);

            (await scenario.WaitInquiry()).Finish();

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry
                .AssertSkip()
                .ApplyAction<RonAction>()
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertRon(3, 2)
                .AssertScore(han: 1)
                .AssertYaku<ShiiaruRaotai>()
            ).Resolve();
        }
        #endregion
    }
}