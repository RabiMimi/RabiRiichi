using RabiRiichi.Communication;
using RabiRiichi.Riichi.Setup;
using RabiRiichi.Util;


namespace RabiRiichi.Riichi {
    public class GameConfig {
        public int playerCount = 2;
        /// <summary> 番缚 </summary>
        public int minHan = 1;

        /// <summary> 初始点数 </summary>
        public int initialPoints = 25000;

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

    public class GameInfo {
        public GameConfig config;
        /// <summary> 游戏状态 </summary>
        public GamePhase phase = GamePhase.Pending;
        /// <summary> 场风 </summary>
        public Wind wind = Wind.E;
        /// <summary> 局数，从0开始 </summary>
        public int round = 0;
        /// <summary> 本场 </summary>
        public int honba = 0;

        /// <summary> 游戏内用时间戳 </summary>
        public AutoIncrementInt timeStamp;

        /// <summary> 事件ID </summary>
        public AutoIncrementInt eventId;

        /// <summary> 庄家 </summary>
        public int Banker => ((int)wind + round) % config.playerCount;

        /// <summary> 是否是本局第一巡，用于判定天地和和W立 </summary>
        public bool firstJun = true;

        public GameInfo(GameConfig config) {
            this.config = config;
        }

        /// <summary> 清空本局数据以开始下一局 </summary>
        public void Reset() {
            firstJun = true;
            timeStamp.Reset();
        }
    }
}