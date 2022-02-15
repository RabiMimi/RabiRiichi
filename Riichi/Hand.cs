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
        /// <summary> 当前玩家 </summary>
        public Player player;
        /// <summary> 当前游戏实例 </summary>
        public Game game => player.game;
        /// <summary> 第一个立直宣告牌 </summary>
        public GameTile riichiTile = null;
        /// <summary> 立直宣告牌 </summary>
        public GameTile riichiIndicator = null;
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

        public GameTile GetTile(Tile tile) => hand.Find(t => t.tile == tile);
        public GameTiles GetTiles(Tiles tiles) {
            var tmp = new Tiles(tiles);
            var ret = new GameTiles();
            foreach (var tile in hand) {
                if (tmp.Contains(tile.tile)) {
                    ret.Add(tile);
                    tmp.Remove(tile.tile);
                }
            }
            Debug.Assert(ret.Count == tiles.Count);
            return ret;
        }

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
            tile.discardTime = player.game.gameInfo.Time();
            discarded.Add(tile);
            if (riichi) {
                Debug.Assert(menzen);
                tile.riichi = true;
                if (!this.riichi) {
                    this.riichi = true;
                    riichiTile = tile;
                    riichiIndicator = tile;
                }
            }
            if (this.riichi && riichiIndicator.source != TileSource.Discard) {
                riichiIndicator = tile;
                tile.riichi = true;
            }
            return this;
        }

        public Hand Remove(GameTile tile) {
            if (hand.Contains(tile)) {
                tile.fromPlayer = player;
                hand.Remove(tile);
            }
            return this;
        }

        public Hand AddGroup(GameTiles tiles, TileSource source) {
            groups.Add(tiles);
            tiles.ForEach(tile => {
                tile.player = player;
                tile.source = source;
                tile.riichi = false;
                Remove(tile);
            });
            return this;
        }
    }
}
