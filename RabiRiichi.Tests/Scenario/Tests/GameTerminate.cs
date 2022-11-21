using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core.Config;
using RabiRiichi.Events.InGame;
using RabiRiichi.Core;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Tests {
    [TestClass]
    public class ScenarioGameTerminate {
        #region Low Score
        private static readonly long[] POINTS_RANGE = new long[] { 10000, 50000 };
        private static async Task<Scenario> BuildLowScoreTest(EndGamePolicy policy, long initialPoints) {
            var scenario = new ScenarioBuilder()
                .WithConfig(config => config
                    .SetEndGamePolicy(policy)
                    .SetPointsRange(POINTS_RANGE))
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

            return scenario;
        }

        [TestMethod]
        public async Task NoTerminate_ExactLowScore() {
            var scenario = await BuildLowScoreTest(
                EndGamePolicy.Default, POINTS_RANGE[0] + 1300);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.AreEqual(POINTS_RANGE[0], ev.game.GetPlayer(0).points);
                })
                .AssertNoEvent<StopGameEvent>()
                .Resolve();
        }

        [TestMethod]
        public async Task Terminate_LowScore() {
            var scenario = await BuildLowScoreTest(
                EndGamePolicy.Default, POINTS_RANGE[0] + 1200);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario
                .AssertEvent<NextGameEvent>(ev => {
                    Assert.IsTrue(POINTS_RANGE[0] > ev.game.GetPlayer(0).points);
                })
                .AssertEvent<StopGameEvent>(ev => {
                    for (int i = 0; i < ev.game.players.Length; i++) {
                        Assert.AreEqual(ev.game.players[i].points, ev.endGamePoints[i]);
                    }
                })
                .Resolve();
        }

        [TestMethod]
        public async Task InstantTerminate_LowScore() {
            var scenario = await BuildLowScoreTest(
                EndGamePolicy.InstantPointsOutOfRange, POINTS_RANGE[0] + 2000);

            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(0, player => player.points = POINTS_RANGE[0] - 100);

            inquiry.ForPlayer(1,
                playerInquiry => playerInquiry.ApplySkip()
            ).AssertAutoFinish();

            await scenario.AssertEvent<StopGameEvent>().Resolve();
        }

        [TestMethod]
        public async Task NoTerminate_AllowLowScore() {
            var scenario = await BuildLowScoreTest(
                EndGamePolicy.Default & ~EndGamePolicy.PointsOutOfRange,
                POINTS_RANGE[0] + 1200);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.IsTrue(ev.game.GetPlayer(0).points < POINTS_RANGE[0]);
                })
                .AssertNoEvent<StopGameEvent>()
                .Resolve();
        }

        #endregion

        #region High Score
        private static async Task<Scenario> BuildHighScoreTest(EndGamePolicy policy, long initialPoints) {
            var scenario = new ScenarioBuilder()
                .WithConfig(config => config
                    .SetEndGamePolicy(policy)
                    .SetPointsRange(POINTS_RANGE))
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("345666789s2234m")
                    .SetRiichiTile("2s")
                    .SetPoints(initialPoints))
                .WithWall(wall => wall
                    .Reserve("2m")
                    .AddDoras("1z")
                    .AddUradoras("1z"))
                .Start(0);

            (await scenario.WaitInquiry()).Finish();

            return scenario;
        }

        [TestMethod]
        public async Task NoTerminate_ExactHighScore() {
            var scenario = await BuildHighScoreTest(
                EndGamePolicy.Default, POINTS_RANGE[1] - 1300);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.AreEqual(POINTS_RANGE[1], ev.game.GetPlayer(1).points);
                })
                .AssertNoEvent<StopGameEvent>()
                .Resolve();
        }

        [TestMethod]
        public async Task Terminate_HighScore() {
            var scenario = await BuildHighScoreTest(
                EndGamePolicy.Default, POINTS_RANGE[1] - 1200);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario
                .AssertEvent<NextGameEvent>(ev => {
                    Assert.IsTrue(POINTS_RANGE[1] < ev.game.GetPlayer(1).points);
                })
                .AssertEvent<StopGameEvent>(ev => {
                    for (int i = 0; i < ev.game.players.Length; i++) {
                        Assert.AreEqual(ev.game.players[i].points, ev.endGamePoints[i]);
                    }
                })
                .Resolve();
        }

        [TestMethod]
        public async Task InstantTerminate_HighScore() {
            var scenario = await BuildHighScoreTest(
                EndGamePolicy.InstantPointsOutOfRange, POINTS_RANGE[1] - 2000);

            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => player.points = POINTS_RANGE[1] + 100);

            inquiry.ForPlayer(1,
                playerInquiry => playerInquiry.ApplySkip()
            ).AssertAutoFinish();

            await scenario.AssertEvent<StopGameEvent>().Resolve();
        }

        [TestMethod]
        public async Task NoTerminate_AllowHighScore() {
            var scenario = await BuildHighScoreTest(
                EndGamePolicy.Default & ~EndGamePolicy.PointsOutOfRange,
                POINTS_RANGE[1] - 1200);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.IsTrue(ev.game.GetPlayer(1).points > POINTS_RANGE[1]);
                })
                .AssertNoEvent<StopGameEvent>()
                .Resolve();
        }

        #endregion

        #region Extended round
        [TestMethod]
        public async Task ExtendedRound_InsufficientPoints() {
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
        public async Task ExtendedRound_InsufficientPoints_ConfigOff() {
            var scenario = new ScenarioBuilder()
                .WithConfig(config => config
                    .SetEndGamePolicy(EndGamePolicy.Default & ~EndGamePolicy.ExtendedRound))
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
        public async Task Terminate_SwitchDealerSufficientPoints() {
            var scenario = new ScenarioBuilder()
                .WithConfig(config => config.SetFinishPoints(30000))
                .WithPlayer(2, player => player.SetPoints(30000))
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
        #endregion

        #region Dealer tenpai
        [TestMethod]
        public async Task Terminate_DealerRyuukyokuSufficientPoints() {
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
        public async Task Terminate_DealerTenpaiSufficientPoints() {
            var scenario = new ScenarioBuilder()
                .WithConfig(config => config.SetFinishPoints(30000))
                .WithPlayer(3, player => player
                    .SetPoints(30000)
                    .SetFreeTiles("1112345678999p"))
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
        public async Task Renchan_DealerTenpaiSufficientPoints_ConfigOff() {
            var scenario = new ScenarioBuilder()
                .WithConfig(config => config
                    .SetFinishPoints(30000)
                    .SetEndGamePolicy(EndGamePolicy.Default & ~EndGamePolicy.DealerTenpai))
                .WithPlayer(3, player => player
                    .SetPoints(30000)
                    .SetFreeTiles("1112345678999p"))
                .WithState(state => state.SetRound(Wind.E, 3, 2))
                .WithWall(wall => wall.Reserve("1s"))
                .Build(1)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).Finish();

            await scenario
                .AssertEvent<NextGameEvent>()
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.AreEqual(0, ev.round);
                    Assert.AreEqual(3, ev.dealer);
                    Assert.AreEqual(3, ev.honba);
                })
                .AssertNoEvent<StopGameEvent>()
                .Resolve();
        }
        #endregion

        #region Dealer agari
        [TestMethod]
        public async Task Terminate_DealerTopsWithSufficientPoints() {
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
        public async Task Renchan_DealerTopsWithSufficientPoints_ConfigOff() {
            var scenario = new ScenarioBuilder()
                .WithConfig(config => config
                    .SetFinishPoints(30000)
                    .SetEndGamePolicy(EndGamePolicy.Default & ~EndGamePolicy.DealerAgari))
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
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.AreEqual(0, ev.round);
                    Assert.AreEqual(3, ev.dealer);
                    Assert.AreEqual(3, ev.honba);
                })
                .AssertNoEvent<StopGameEvent>()
                .Resolve();
        }

        [TestMethod]
        public async Task Renchan_DealerNotTopWithSufficientPoints() {
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