using RabiRiichi.Communication;
using RabiRiichi.Core.Setup;
using RabiRiichi.Util;


namespace RabiRiichi.Core {

    public class GameConfig : IRabiMessage {
        public RabiMessageType msgType => RabiMessageType.Unnecessary;
        /// <summary> 玩家数 </summary>
        [RabiBroadcast] public int playerCount = 2;

        /// <summary> 番缚 </summary>
        [RabiBroadcast] public int minHan = 1;

        /// <summary> 初始点数 </summary>
        [RabiBroadcast] public int initialPoints = 25000;

        /// <summary> 立直棒点数 </summary>
        [RabiBroadcast] public int riichiPoints = 1000;

        /// <summary> 场棒点数 </summary>
        [RabiBroadcast] public int honbaPoints = 300;

        /// <summary> (西)入点数 </summary>
        [RabiBroadcast] public int finishPoints = 30000;

        /// <summary> 食断 </summary>
        [RabiBroadcast] public bool allowKuitan = true;

        /// <summary> 几庄战 </summary>
        [RabiBroadcast] public int totalRound = 1;

        /// <summary> 注册类 </summary>
        public BaseSetup setup = new RiichiSetup();

        /// <summary> 交互类 </summary>
        public IActionCenter actionCenter = null;

        /// <summary> 随机种子 </summary>
        public int? seed;
    }

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
        [RabiBroadcast] public int banker = 0;
        /// <summary> 本场 </summary>
        [RabiBroadcast] public int honba = 0;
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