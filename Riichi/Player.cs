namespace RabiRiichi.Riichi {
    public enum Wind {
        E, S, W, N
    }
    public class Player {
        public int id;
        public Game game;
        public Wind wind;
        /// <summary> 场上立直棒数量，不可用于判定是否立直 </summary>
        public int riichiStick = 0;
        /// <summary> 手牌 </summary>
        public Hand hand = new Hand();

        public Player(int id, Game game) {
            this.id = id;
            this.game = game;
            hand.player = this;
        }
        
        public int NextPlayerId => game.NextPlayerId(id);
        public int PrevPlayerId => game.PrevPlayerId(id);
        public Player NextPlayer => game.GetPlayer(NextPlayerId);
        public Player PrevPlayer => game.GetPlayer(PrevPlayerId);
        public bool samePlayer(Player rhs) => id == rhs.id;
    }
}
