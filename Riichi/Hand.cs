using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        /// <summary> 当前玩家 </summary>
        public int player = -1;
        public int PrevPlayer => game.PrevPlayer(player);
        public int NextPlayer => game.NextPlayer(player);
        /// <summary> 第一个立直牌 </summary>
        public GameTile riichiTile = null;
        /// <summary> 立直 </summary>
        public bool riichi = false;
        /// <summary> 门清 </summary>
        public bool menzen = true;
        /// <summary> 振听 </summary>
        public bool furiten = false;
        /// <summary> 和 </summary>
        public GameTile ron = null;
        public bool IsRon => ron != null;
        /// <summary> 牌的总数，注意：杠会被算作3张牌 </summary>
        public int Count => groups.Select(gr => Math.Min(3, gr.Count)).Sum() + hand.Count;

        public Hand Add(GameTile tile) {
            tile.player = player;
            tile.source = TileSource.Hand;
            hand.Add(tile);
            return this;
        }

        public Hand Play(GameTile tile, bool riichi = false) {
            tile.fromPlayer = player;
            tile.source = TileSource.Discard;
            hand.Remove(tile);
            tile.discardTime = game.Time();
            discarded.Add(tile);
            if (riichi) {
                Debug.Assert(menzen);
                tile.riichi = true;
                if (!this.riichi) {
                    this.riichi = true;
                    riichiTile = tile;
                }
            }
            return this;
        }

        public Hand Remove(GameTile tile) {
            tile.fromPlayer = player;
            hand.Remove(tile);
            return this;
        }

        public Hand AddGroup(GameTiles tiles, TileSource source) {
            groups.Add(tiles);
            tiles.ForEach(tile => {
                tile.player = player;
                tile.source = source;
            });
            return this;
        }
    }
}
