using RabiRiichi.Communication;

namespace RabiRiichi.Core.Config {
    public class PointThreshold : IRabiMessage {
        public RabiMessageType msgType => RabiMessageType.Unnecessary;
        /// <summary> 初始点数 </summary>
        [RabiBroadcast] public int initialPoints = 25000;

        /// <summary> 立直棒点数 </summary>
        [RabiBroadcast] public int riichiPoints = 1000;

        /// <summary> 场棒点数 </summary>
        [RabiBroadcast] public int honbaPoints = 300;

        /// <summary> 终局点数 </summary>
        [RabiBroadcast] public int finishPoints = 30000;

        /// <summary> 不听罚符（单人/多人） </summary>
        [RabiBroadcast] public int[] ryuukyokuPoints = new int[] { 1000, 1500 };

        /// <summary> 天边 </summary>
        [RabiBroadcast] public int[] validPointsRange = new int[] { 0, 1000000 };
        public bool ArePointsValid(int points) {
            return points >= validPointsRange[0] && points <= validPointsRange[1];
        }
    }
}