namespace RabiRiichi.Riichi {
    public class GameConfig {
        public int playerCount = 2;
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
        /// <summary> 局数 </summary>
        public int round = 0;
        /// <summary> 本场 </summary>
        public int honba = 0;
        /// <summary> 庄家 </summary>
        public int Banker => ((int)wind + round) % config.playerCount;

        public GameInfo(GameConfig config) {
            this.config = config;
        }
    }
}