using RabiRiichi.Communication;
using RabiRiichi.Core.Setup;
using RabiRiichi.Generated.Core.Config;
using RabiRiichi.Utils;

namespace RabiRiichi.Core.Config {
    [RabiMessage]
    public class GameConfig {
        #region Config
        /// <summary> 玩家数 </summary>
        [RabiBroadcast] public int playerCount = 2;

        /// <summary> 几庄战 </summary>
        [RabiBroadcast] public int totalRound = 1;

        /// <summary> 番缚 </summary>
        [RabiBroadcast] public int minHan = 1;

        /// <summary> 初始牌山 </summary>
        public Tiles initialTiles = Tiles.All.Value;

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

        /// <summary> 和牌选项 </summary>
        [RabiBroadcast] public AgariOption agariOption = AgariOption.Default;

        /// <summary> 计分选项 </summary>
        [RabiBroadcast] public ScoringOption scoringOption = ScoringOption.Default;

        /// <summary> 中途流局 </summary>
        [RabiBroadcast] public RyuukyokuTrigger ryuukyokuTrigger = RyuukyokuTrigger.Default;
        #endregion

        #region Helper methods
        public long HonbaPointsForOnePlayer(int honba) {
            return (pointThreshold.honbaPoints / (playerCount - 1) * honba).CeilTo100();
        }

        public long MinRiichiPoints {
            get {
                if (riichiPolicy.HasFlag(RiichiPolicy.SufficientPoints)) {
                    return pointThreshold.validPointsRange[0] + pointThreshold.riichiPoints;
                }
                if (riichiPolicy.HasFlag(RiichiPolicy.ValidPoints)) {
                    return pointThreshold.validPointsRange[0];
                }
                return long.MinValue;
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

        public GameConfigMsg ToProto() {
            return new GameConfigMsg {
                PlayerCount = playerCount,
                TotalRound = totalRound,
                MinHan = minHan,
                PointThreshold = pointThreshold.ToProto(),
                RenchanPolicy = (int)renchanPolicy,
                EndGamePolicy = (int)endGamePolicy,
                KuikaePolicy = (int)kuikaePolicy,
                RiichiPolicy = (int)riichiPolicy,
                DoraOption = (int)doraOption,
                AgariOption = (int)agariOption,
                ScoringOption = (int)scoringOption,
                RyuukyokuTrigger = (int)ryuukyokuTrigger,
                Seed = seed ?? 0,
            };
        }
        #endregion
    }
}