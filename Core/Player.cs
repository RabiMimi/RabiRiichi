using RabiRiichi.Generated.Core;

namespace RabiRiichi.Core {
    public class Player {
        public int id;
        public Game game;
        /// <summary> 本局游戏中该玩家的自风 </summary>
        public Wind Wind => (Wind)game.Dist(game.info.dealer, id);
        /// <summary> 手牌 </summary>
        public Hand hand;
        /// <summary> 点数 </summary>
        public long points;

        public Player(int id, Game game) {
            this.id = id;
            this.game = game;
            points = game.config.pointThreshold.initialPoints;
        }

        public int NextPlayerId => game.NextPlayerId(id);
        public int PrevPlayerId => game.PrevPlayerId(id);
        public Player NextPlayer => game.GetPlayer(NextPlayerId);
        public Player PrevPlayer => game.GetPlayer(PrevPlayerId);

        /// <summary> 是否是同一个玩家 </summary>
        public bool SamePlayer(Player rhs) => rhs != null && id == rhs.id;

        /// <summary> 是否是庄家 </summary>
        public bool IsDealer => id == game.info.dealer;

        /// <summary> 是否是役牌 </summary>
        public virtual bool IsYaku(Tile tile) => game.IsYaku(tile) || tile.IsSame(Tile.From(Wind));

        /// <summary> 计算rhs是该玩家后的第几个 </summary>
        public int Dist(int rhsId) => game.Dist(id, rhsId);
        /// <summary> 计算rhs是该玩家后的第几个 </summary>
        public int Dist(Player rhs) => game.Dist(id, rhs.id);

        /// <summary> 开局时重置玩家手牌状态 </summary>
        public void Reset() {
            hand = new Hand() {
                player = this
            };
        }
    }
}
