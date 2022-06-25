using RabiRiichi.Communication;
using RabiRiichi.Generated.Core.Config;

namespace RabiRiichi.Core.Config {
    [RabiMessage]
    public class PointThreshold {
        /// <summary> 初始点数 </summary>
        [RabiBroadcast] public long initialPoints = 25000;

        /// <summary> 立直棒点数 </summary>
        [RabiBroadcast] public long riichiPoints = 1000;

        /// <summary> 场棒点数 </summary>
        [RabiBroadcast] public long honbaPoints = 300;

        /// <summary> 终局点数 </summary>
        [RabiBroadcast] public long finishPoints = 30000;

        /// <summary> 不听罚符（单人/多人） </summary>
        [RabiBroadcast] public long[] ryuukyokuPoints = new long[] { 1000, 1500 };

        /// <summary> 天边 </summary>
        [RabiBroadcast] public long[] validPointsRange = new long[] { 0, 1000000 };

        #region Helper methods
        public bool ArePointsValid(long points)
            => points >= validPointsRange[0] && points <= validPointsRange[1];
        #endregion

        public PointThresholdMsg ToProto() {
            var ret = new PointThresholdMsg {
                InitialPoints = initialPoints,
                RiichiPoints = riichiPoints,
                HonbaPoints = honbaPoints,
                FinishPoints = finishPoints,
            };
            ret.RyuukyokuPoints.AddRange(ryuukyokuPoints);
            ret.ValidPointsRange.AddRange(validPointsRange);
            return ret;
        }
    }
}