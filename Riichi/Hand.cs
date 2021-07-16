using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Riichi {
    public class Hand {
        /// <summary> 所有手牌 </summary>
        public GameTiles hand = new GameTiles();
        /// <summary> 吃碰杠 </summary>
        public List<GameTiles> groups = new List<GameTiles>();
        /// <summary> 牌河 </summary>
        public GameTiles discarded = new GameTiles();

        public Game game;
        public sbyte player = -1;
        /// <summary> 立直 </summary>
        public bool riichi = false;
        /// <summary> 门清 </summary>
        public bool menzen = true;
        /// <summary> 和 </summary>
        public GameTile ron = null;
        public bool IsRon => ron != null;
        /// <summary> 牌的总数，注意：杠会被算作3张牌 </summary>
        public int Count => groups.Select(gr => Math.Min(3, gr.Count)).Sum() + hand.Count;
        /// <summary> 听牌 </summary>
        public List<GameTiles> tenpai = new List<GameTiles>();

        public Hand Add(GameTile tile) {
            tile.player = player;
            tile.source = TileSource.Hand;
            hand.Add(tile);
            return this;
        }

        public Hand Play(GameTile tile) {
            tile.fromPlayer = player;
            tile.source = TileSource.Discard;
            hand.Remove(tile);
            discarded.Add(tile);
            return this;
        }

        public Hand Remove(GameTile tile) {
            tile.fromPlayer = player;
            hand.Remove(tile);
            return this;
        }

        public Hand AddGroup(GameTiles tiles) {
            groups.Add(tiles);
            return this;
        }
    }
}
