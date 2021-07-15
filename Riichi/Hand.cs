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

        public sbyte player;
        /// <summary> 立直 </summary>
        public bool riichi = false;
        /// <summary> 门清 </summary>
        public bool menzen = true;
        /// <summary> 和 </summary>
        public GameTile ron = null;
        public bool IsRon => ron != null;
        public int Count => groups.Select(gr => gr.Count).Sum() + hand.Count;
        /// <summary> 听牌 </summary>
        public List<GameTiles> tenpai = new List<GameTiles>();
        /// <summary> 按牌数量计数 </summary>
        public byte[] cnt = new byte[256];

        public void Add(GameTile tile) {
            tile.player = player;
            tile.source = TileSource.Hand;
            hand.Add(tile);
            cnt[tile.tile.Val]++;
        }

        public void Play(GameTile tile) {
            tile.fromPlayer = player;
            tile.source = TileSource.Discard;
            hand.Remove(tile);
            cnt[tile.tile.Val]--;
        }
    }
}
