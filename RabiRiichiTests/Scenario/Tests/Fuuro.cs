using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Core.Config;
using RabiRiichi.Event.InGame;
using RabiRiichiTests.Helper;
using System;
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

        private async Task<Scenario> MultiChoiceChiiScenario(string expectedDiscardTiles, Action<ScenarioBuilder> setup = null) {
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
                    return true;
                })
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            scenario.AssertNoEvent<DrawTileEvent>();

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>(action => {
                        action.options.ToTiles().AssertEquals(expectedDiscardTiles);
                        return true;
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
    }
}