using RabiRiichi.Communication;
using RabiRiichi.Core.Config;
using RabiRiichi.Util;

namespace RabiRiichi.Core {
    public enum GamePhase {
        Pending, Running, Finished
    }

    public class GameInfo : IRabiMessage {
        public RabiMessageType msgType => RabiMessageType.Unnecessary;
        public GameConfig config = new();
        /// <summary> 游戏状态 </summary>
        public GamePhase phase = GamePhase.Pending;
        /// <summary> 第几轮 </summary>
        [RabiBroadcast] public int round = 0;
        /// <summary> 场风 </summary>
        [RabiBroadcast] public Wind wind => (Wind)(round % config.playerCount);
        /// <summary> 庄家ID </summary>
        [RabiBroadcast] public int dealer = 0;
        /// <summary> 本场 </summary>
        [RabiBroadcast] public int honba = 0;
        /// <summary> 是否是All Last </summary>
        public bool IsAllLast {
            get {
                if (round == config.totalRound - 1) {
                    return dealer == config.playerCount - 1;
                }
                return round >= config.totalRound;
            }
        }
        /// <summary> 立直棒数量 </summary>
        [RabiBroadcast] public int riichiStick = 0;

        /// <summary> 游戏内用时间戳 </summary>
        public AutoIncrementInt timeStamp;

        /// <summary> 事件ID </summary>
        public AutoIncrementInt eventId;

        public GameInfo(GameConfig config) {
            this.config = config;
        }

        /// <summary> 清空本局数据以开始下一局 </summary>
        public void Reset() {
            timeStamp.Reset();
        }
    }
}