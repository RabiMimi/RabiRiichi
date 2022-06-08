using RabiRiichi.Communication;
using RabiRiichi.Core.Setup;
using RabiRiichi.Util;

namespace RabiRiichi.Core.Config {
    public class GameConfig : IRabiMessage {
        public RabiMessageType msgType => RabiMessageType.Unnecessary;
        #region Config
        /// <summary> 玩家数 </summary>
        [RabiBroadcast] public int playerCount = 2;

        /// <summary> 几庄战 </summary>
        [RabiBroadcast] public int totalRound = 1;

        /// <summary> 番缚 </summary>
        [RabiBroadcast] public int minHan = 1;

        /// <summary> 食断 </summary>
        [RabiBroadcast] public bool allowKuitan = true;

        /// <summary> 包牌 </summary>
        [RabiBroadcast] public bool allowPao = true;

        /// <summary> 分数线 </summary>
        [RabiBroadcast] public PointThreshold pointThreshold = new();

        /// <summary> 连庄策略 </summary>
        [RabiBroadcast] public RenchanPolicy renchanPolicy = RenchanPolicy.Default;

        /// <summary> 终局策略 </summary>
        [RabiBroadcast] public EndGamePolicy endGamePolicy = EndGamePolicy.Default;

        /// <summary> 食替策略 </summary>
        [RabiBroadcast] public KuikaePolicy kuikaePolicy = KuikaePolicy.Default;

        /// <summary> 立直策略 </summary>
        [RabiBroadcast] public RiichiPolicy riichiPolicy = RiichiPolicy.Default;

        /// <summary> 宝牌选项 </summary>
        [RabiBroadcast] public DoraOption doraOption = DoraOption.Default;

        /// <summary> 中途流局 </summary>
        [RabiBroadcast] public RyuukyokuTrigger ryuukyokuTrigger = RyuukyokuTrigger.Default;
        #endregion

        #region Helper methods
        public int HonbaPointsForOnePlayer(int honba) {
            return (pointThreshold.honbaPoints / (playerCount - 1) * honba).CeilTo100();
        }

        public int MinRiichiPoints {
            get {
                if (riichiPolicy.HasFlag(RiichiPolicy.SufficientPoints)) {
                    return pointThreshold.validPointsRange[0] + pointThreshold.riichiPoints;
                }
                if (riichiPolicy.HasFlag(RiichiPolicy.ValidPoints)) {
                    return pointThreshold.validPointsRange[0];
                }
                return int.MinValue;
            }
        }
        #endregion

        #region For Developer Use
        /// <summary> 注册类 </summary>
        public BaseSetup setup = new RiichiSetup();

        /// <summary> 交互类 </summary>
        public IActionCenter actionCenter = null;

        /// <summary> 随机种子 </summary>
        public ulong? seed;
        #endregion
    }
}