using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RabiRiichi.Pattern;

namespace RabiRiichi.Riichi {
    public class Hand {
        /// <summary> 所有手牌 </summary>
        public GameTiles allTiles = new GameTiles();
        /// <summary> 吃碰杠 </summary>
        public List<GameTiles> groups = new List<GameTiles>();
        /// <summary> 牌河 </summary>
        public GameTiles discarded = new GameTiles();
        /// <summary> 当前玩家 </summary>
        public Player player;
        /// <summary> 当前游戏实例 </summary>
        public Game game => player.game;
        /// <summary> 第一个立直宣告牌 </summary>
        public GameTile firstRiichiTile = null;
        /// <summary> 立直 </summary>
        public bool riichi = false;
        /// <summary> 门清 </summary>
        public bool menzen = true;
        /// <summary> 获取听的牌 </summary>
        /// <param name="hand">必须是13张</param>
        /// <returns>听牌列表，无赤宝牌</returns>
        public Tiles GetTenpai {
            get {
                var ret = new Tiles();
                allTiles.Sort();
                foreach (var pattern in Patterns.BasePatterns) {
                    int shanten = pattern.Shanten(this, null, out var tiles, 0);
                    Debug.Assert(shanten >= 0);
                    if (shanten > 0) {
                        continue;
                    }
                    ret.AddRange(tiles);
                }
                ret = new Tiles(ret.Distinct());
                ret.Sort();
                return ret;
            }
        }

        /// <summary> 是否振听 </summary>
        public bool IsFuriten {
            get {
                // TODO: 选择一个实现：
                // 1. 在所有玩家操作以后再将牌加入弃牌列表
                // 2. 在玩家打出牌时即将其加入弃牌表，但是在判定振听时忽略
                var tenpai = GetTenpai;
                // 摸切振听
                if (discarded.Any(tile => tenpai.Contains(tile.tile.WithoutDora))) {
                    return true;
                }
                if (riichi) {
                    // 立直振听
                    return game.AllDiscardedTiles
                        .Where(tile => tile.discardTime >= firstRiichiTile.discardTime)
                        .Any(tile => tenpai.Contains(tile.tile.WithoutDora));
                } else {
                    // 同巡振听
                    var discarded = game.AllDiscardedTiles.ToList();
                    int lastIndex = discarded.FindLastIndex(tile => tile.fromPlayer == player);
                    return discarded.Skip(lastIndex + 1).Any(tile => tenpai.Contains(tile.tile.WithoutDora));
                }
            }
        }

        /// <summary> 和 </summary>
        public GameTile ron = null;
        public bool IsRon => ron != null;
        /// <summary> 牌的总数，注意：杠会被算作3张牌 </summary>
        public int Count => groups.Select(gr => Math.Min(3, gr.Count)).Sum() + allTiles.Count;

        public GameTile GetTile(Tile tile) => allTiles.Find(t => t.tile == tile);
        public GameTiles GetTiles(Tiles tiles) {
            var tmp = new Tiles(tiles);
            var ret = new GameTiles();
            foreach (var tile in allTiles) {
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
            allTiles.Add(tile);
            return this;
        }

        public Hand Play(GameTile tile, bool riichi = false) {
            tile.fromPlayer = player;
            tile.source = TileSource.Discard;
            allTiles.Remove(tile);
            tile.discardTime = player.game.gameInfo.Time();
            discarded.Add(tile);
            if (riichi) {
                Debug.Assert(menzen);
                tile.riichi = true;
                if (!this.riichi) {
                    this.riichi = true;
                    firstRiichiTile = tile;
                }
            }
            return this;
        }

        public Hand Remove(GameTile tile) {
            if (allTiles.Contains(tile)) {
                tile.fromPlayer = player;
                allTiles.Remove(tile);
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
