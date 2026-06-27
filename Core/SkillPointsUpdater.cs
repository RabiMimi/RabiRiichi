using RabiRiichi.Core.Config;

namespace RabiRiichi.Core {
    public class SkillPointsUpdater {
        private readonly GameConfig _config;

        public SkillPointsUpdater(GameConfig config) {
            _config = config;
        }

        /// <summary>
        /// 检查玩家是否有足够的分数支付
        /// </summary>
        public bool CanDeduct(Player player, long cost) {
            var policy = _config.pointsDeductionPolicy;
            if (policy == PointsDeductionPolicy.AlwaysAllow) {
                return true;
            }
            if (policy == PointsDeductionPolicy.AlwaysBlock) {
                return false;
            }

            var threshold = _config.pointThreshold;
            if (policy == PointsDeductionPolicy.SufficientPoints) {
                return threshold.ArePointsValid(player.points - cost);
            }
            if (policy == PointsDeductionPolicy.ValidPoints) {
                return threshold.ArePointsValid(player.points);
            }

            return false;
        }

        /// <summary>
        /// 尝试扣减玩家的分数。如果 forced 为 true，则即使检查不通过也强制更新。
        /// 返回是否成功更新。
        /// </summary>
        public bool TryDeduct(Player player, long cost, bool forced = false) {
            if (forced || CanDeduct(player, cost)) {
                player.points -= cost;
                return true;
            }
            return false;
        }
    }
}
