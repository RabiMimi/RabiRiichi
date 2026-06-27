using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Tests.Helper;

namespace RabiRiichi.Tests.Core {
    [TestClass]
    public class SkillPointsUpdaterTest {
        private Game CreateGameWithPolicy(PointsDeductionPolicy policy, long? minPoints = null, long? maxPoints = null) {
            var config = new GameConfig {
                actionCenter = new JsonStringActionCenter(null),
                pointsDeductionPolicy = policy
            };
            if (minPoints.HasValue) {
                config.pointThreshold.validPointsRange[0] = minPoints.Value;
            }
            if (maxPoints.HasValue) {
                config.pointThreshold.validPointsRange[1] = maxPoints.Value;
            }
            return new Game(config);
        }

        [TestMethod]
        public void TestDiRegistration() {
            var game = TestHelper.CreateGame();
            var updater = game.Get<SkillPointsUpdater>();
            Assert.IsNotNull(updater);
        }

        [TestMethod]
        public void TestAlwaysAllowPolicy() {
            var game = CreateGameWithPolicy(PointsDeductionPolicy.AlwaysAllow, 0, 100000);
            var updater = game.Get<SkillPointsUpdater>();
            var player = game.GetPlayer(0);

            player.points = 25000;
            Assert.IsTrue(updater.CanDeduct(player, 1000));
            Assert.IsTrue(updater.TryDeduct(player, 1000));
            Assert.AreEqual(24000, player.points);

            // Even if the deduction goes below 0, it should be allowed under AlwaysAllow
            player.points = 500;
            Assert.IsTrue(updater.CanDeduct(player, 1000));
            Assert.IsTrue(updater.TryDeduct(player, 1000));
            Assert.AreEqual(-500, player.points);
        }

        [TestMethod]
        public void TestAlwaysBlockPolicy() {
            var game = CreateGameWithPolicy(PointsDeductionPolicy.AlwaysBlock, 0, 100000);
            var updater = game.Get<SkillPointsUpdater>();
            var player = game.GetPlayer(0);

            player.points = 25000;
            Assert.IsFalse(updater.CanDeduct(player, 1000));
            Assert.IsFalse(updater.TryDeduct(player, 1000));
            Assert.AreEqual(25000, player.points);

            // If forced is true, it should update anyway
            Assert.IsTrue(updater.TryDeduct(player, 1000, forced: true));
            Assert.AreEqual(24000, player.points);
        }

        [TestMethod]
        public void TestSufficientPointsPolicy() {
            var game = CreateGameWithPolicy(PointsDeductionPolicy.SufficientPoints, 0, 100000);
            var updater = game.Get<SkillPointsUpdater>();
            var player = game.GetPlayer(0);

            // Sufficient points
            player.points = 25000;
            Assert.IsTrue(updater.CanDeduct(player, 1000));
            Assert.IsTrue(updater.TryDeduct(player, 1000));
            Assert.AreEqual(24000, player.points);

            // Insufficient points (goes below minPoints: 0)
            player.points = 500;
            Assert.IsFalse(updater.CanDeduct(player, 1000));
            Assert.IsFalse(updater.TryDeduct(player, 1000));
            Assert.AreEqual(500, player.points);

            // Insufficient points, but forced update
            Assert.IsTrue(updater.TryDeduct(player, 1000, forced: true));
            Assert.AreEqual(-500, player.points);
        }

        [TestMethod]
        public void TestValidPointsPolicy() {
            var game = CreateGameWithPolicy(PointsDeductionPolicy.ValidPoints, 0, 100000);
            var updater = game.Get<SkillPointsUpdater>();
            var player = game.GetPlayer(0);

            // Current points are valid (cost is allowed even if the resulting points go below 0)
            player.points = 500;
            Assert.IsTrue(updater.CanDeduct(player, 1000));
            Assert.IsTrue(updater.TryDeduct(player, 1000));
            Assert.AreEqual(-500, player.points);

            // Current points are invalid (already below 0)
            player.points = -100;
            Assert.IsFalse(updater.CanDeduct(player, 1000));
            Assert.IsFalse(updater.TryDeduct(player, 1000));
            Assert.AreEqual(-100, player.points);

            // Current points are invalid, but forced update
            Assert.IsTrue(updater.TryDeduct(player, 1000, forced: true));
            Assert.AreEqual(-1100, player.points);
        }
    }
}
