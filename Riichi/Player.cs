namespace RabiRiichi.Riichi {
    public enum Wind {
        E, S, W, N
    }
    public class Player {
        public int id;
        public Game game;
        public Wind wind;
        /// <summary> 手牌 </summary>
        public Hand hand = new();

        public Player(int id, Game game) {
            this.id = id;
            this.game = game;
            hand.player = this;
        }

        public int NextPlayerId => game.NextPlayerId(id);
        public int PrevPlayerId => game.PrevPlayerId(id);
        public Player NextPlayer => game.GetPlayer(NextPlayerId);
        public Player PrevPlayer => game.GetPlayer(PrevPlayerId);

        /// <summary> 是否是同一个玩家 </summary>
        public bool SamePlayer(Player rhs) => rhs != null && id == rhs.id;

        /// <summary> 是否是役牌 </summary>
        public virtual bool IsYaku(Tile tile) => game.IsYaku(tile) || tile.IsSame(Tile.From(wind));

        /// <summary> 计算rhs是该玩家后的第几个 </summary>
        public int Dist(Player rhs) {
            int dist = rhs.id - id;
            if (dist < 0) {
                dist += game.players.Length;
            }
            return dist;
        }

        /// <summary> 开局时重置玩家手牌状态 </summary>
        public void Reset() {
            hand = new Hand();
        }
    }
}
