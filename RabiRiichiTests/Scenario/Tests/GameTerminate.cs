using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Core.Config;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using System.Threading.Tasks;

namespace RabiRiichiTests.Scenario.Tests {
    [TestClass]
    public class ScenarioGameTerminate {
        #region Negative Score
        private static async Task<Scenario> BuildNegativeScore(ContinuationOption option, int initialPoints) {
            var scenario = new ScenarioBuilder()
                .WithConfig(config => config.SetContinuationOption(option))
                .WithPlayer(0, playerBuilder => playerBuilder
                    .SetPoints(initialPoints))
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("345666789s2234m")
                    .SetRiichiTile("2s"))
                .WithWall(wall => wall
                    .Reserve("2m")
                    .AddDoras("1z")
                    .AddUradoras("1z"))
                .Start(0);

            (await scenario.WaitInquiry()).Finish();

            return scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertRon(0, 1)
                .AssertScore(han: 1, fu: 40)
                .AssertYaku<Riichi>()
            );
        }

        [TestMethod]
        public async Task NoTerminate_ZeroScore() {
            var scenario = await BuildNegativeScore(
                ContinuationOption.Default, 1300);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.AreEqual(0, ev.game.GetPlayer(0).points);
                })
                .AssertNoEvent<StopGameEvent>()
                .Resolve();
        }

        [TestMethod]
        public async Task Terminate_NegativeScore() {
            var scenario = await BuildNegativeScore(
                ContinuationOption.Default, 1200);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario
                .AssertEvent<NextGameEvent>(ev => {
                    Assert.IsTrue(0 > ev.game.GetPlayer(0).points);
                })
                .AssertEvent<StopGameEvent>(ev => {
                    for (int i = 0; i < ev.game.players.Length; i++) {
                        Assert.AreEqual(ev.game.players[i].points, ev.endGamePoints[i]);
                    }
                })
                .Resolve();
        }

        [TestMethod]
        public async Task InstantTerminate_NegativeScore() {
            var scenario = await BuildNegativeScore(
                ContinuationOption.InstantTerminateOnNegativeScore, 2000);

            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(0, player => player.points = -100);

            inquiry.ForPlayer(1,
                playerInquiry => playerInquiry.ApplySkip()
            ).AssertAutoFinish();

            await scenario.AssertEvent<StopGameEvent>().Resolve();
        }

        [TestMethod]
        public async Task NoTerminate_AllowNegativeScore() {
            var scenario = await BuildNegativeScore(
                ContinuationOption.Default & ~ContinuationOption.TerminateOnNegativeScore, 1200);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.IsTrue(0 > ev.game.GetPlayer(0).points);
                })
                .AssertNoEvent<StopGameEvent>()
                .Resolve();
        }

        #endregion
    }
}