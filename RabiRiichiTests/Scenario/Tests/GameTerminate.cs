using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using System.Threading.Tasks;

namespace RabiRiichiTests.Scenario.Tests {
    [TestClass]
    public class ScenarioGameTerminate {
        #region Low Score
        private const int SUDDEN_DEATH_PT = 10000;
        private static async Task<Scenario> BuildLowScore(EndGamePolicy policy, int initialPoints) {
            initialPoints += SUDDEN_DEATH_PT;
            var scenario = new ScenarioBuilder()
                .WithConfig(config => config
                    .SetEndGamePolicy(policy)
                    .SetSuddenDeathPoints(SUDDEN_DEATH_PT))
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
            var scenario = await BuildLowScore(
                EndGamePolicy.Default, 1300);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.AreEqual(SUDDEN_DEATH_PT, ev.game.GetPlayer(0).points);
                })
                .AssertNoEvent<StopGameEvent>()
                .Resolve();
        }

        [TestMethod]
        public async Task Terminate_NegativeScore() {
            var scenario = await BuildLowScore(
                EndGamePolicy.Default, 1200);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario
                .AssertEvent<NextGameEvent>(ev => {
                    Assert.IsTrue(SUDDEN_DEATH_PT > ev.game.GetPlayer(0).points);
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
            var scenario = await BuildLowScore(
                EndGamePolicy.InstantTerminate, 2000);

            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(0, player => player.points = -100);

            inquiry.ForPlayer(1,
                playerInquiry => playerInquiry.ApplySkip()
            ).AssertAutoFinish();

            await scenario.AssertEvent<StopGameEvent>().Resolve();
        }

        [TestMethod]
        public async Task NoTerminate_AllowNegativeScore() {
            var scenario = await BuildLowScore(
                EndGamePolicy.Default & ~EndGamePolicy.TerminateOnApply, 1200);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.IsTrue(ev.game.GetPlayer(0).points < SUDDEN_DEATH_PT);
                })
                .AssertNoEvent<StopGameEvent>()
                .Resolve();
        }

        #endregion

        #region Extra round
        [TestMethod]
        public async Task ExtraRound_InsufficientPoints() {
            var scenario = new ScenarioBuilder()
                .WithState(state => state.SetRound(Wind.E, 3, 2))
                .WithWall(wall => wall.Reserve("1s"))
                .Build(1)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).Finish();

            await scenario
                .AssertEvent<NextGameEvent>()
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.AreEqual(1, ev.round);
                    Assert.AreEqual(0, ev.dealer);
                    Assert.AreEqual(3, ev.honba);
                })
                .AssertNoEvent<StopGameEvent>()
                .Resolve();
        }

        [TestMethod]
        public async Task Terminate_InsufficientPointsAfterFullRound() {
            var scenario = new ScenarioBuilder()
                .WithState(state => state.SetRound(Wind.S, 3, 2))
                .WithWall(wall => wall.Reserve("1s"))
                .Build(1)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).Finish();

            await scenario
                .AssertEvent<NextGameEvent>()
                .AssertEvent<StopGameEvent>()
                .AssertNoEvent<BeginGameEvent>()
                .Resolve();
        }

        [TestMethod]
        public async Task Terminate_SufficientPoints() {
            var scenario = new ScenarioBuilder()
                .WithConfig(config => config.SetFinishPoints(30000))
                .WithPlayer(3, player => player.SetPoints(30000))
                .WithState(state => state.SetRound(Wind.E, 3, 2))
                .WithWall(wall => wall.Reserve("1s"))
                .Build(1)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).Finish();

            await scenario
                .AssertEvent<NextGameEvent>()
                .AssertEvent<StopGameEvent>()
                .AssertNoEvent<BeginGameEvent>()
                .Resolve();
        }

        [TestMethod]
        public async Task Terminate_DealerWinsWithSufficientPoints() {
            var scenario = new ScenarioBuilder()
                .WithConfig(config => config.SetFinishPoints(30000))
                .WithState(state => state.SetRound(Wind.E, 3, 2))
                .WithPlayer(3, playerBuilder => playerBuilder
                    .SetFreeTiles("1123456789s444z"))
                .WithWall(wall => wall.Reserve("1s"))
                .Build(3)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).ForPlayer(3, playerInquiry => {
                playerInquiry.ApplyAction<TsumoAction>();
            }).AssertAutoFinish();

            await scenario
                .AssertEvent<NextGameEvent>(ev => {
                    Assert.IsTrue(ev.game.GetPlayer(3).points >= 30000);
                })
                .AssertEvent<StopGameEvent>()
                .AssertNoEvent<BeginGameEvent>()
                .Resolve();
        }


        [TestMethod]
        public async Task Renchan_DealerDoesNotWinWithSufficientPoints() {
            var scenario = new ScenarioBuilder()
                .WithConfig(config => config.SetFinishPoints(30000))
                .WithState(state => state.SetRound(Wind.E, 3, 2))
                .WithPlayer(3, playerBuilder => playerBuilder
                    .SetFreeTiles("1123456789s444z"))
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetPoints(90000))
                .WithWall(wall => wall.Reserve("1s"))
                .Build(3)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).ForPlayer(3, playerInquiry => {
                playerInquiry.ApplyAction<TsumoAction>();
            }).AssertAutoFinish();

            await scenario
                .AssertEvent<NextGameEvent>(ev => {
                    Assert.IsTrue(ev.game.GetPlayer(3).points >= 30000);
                })
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.AreEqual(0, ev.round);
                    Assert.AreEqual(3, ev.dealer);
                    Assert.AreEqual(3, ev.honba);
                })
                .AssertNoEvent<StopGameEvent>()
                .Resolve();
        }
        #endregion
    }
}