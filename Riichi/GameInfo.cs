using RabiRiichi.Action;
using RabiRiichi.Setup;

namespace RabiRiichi.Riichi {
    public class GameConfig {
        public int playerCount = 2;
        public BaseSetup setup = new RiichiSetup();
        public System.Action<MultiPlayerInquiry> inquiryCallback = null;
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
        public int timeStamp = 0;
        public int Time(bool advance = true) => timeStamp += advance ? 1 : 0;

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
            timeStamp = 0;
        }
    }
}