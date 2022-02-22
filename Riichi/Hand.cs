using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RabiRiichi.Pattern;

namespace RabiRiichi.Riichi {
    public class Hand {
        /// <summary> 手牌（不包含副露） </summary>
        public GameTiles freeTiles = new GameTiles();

        /// <summary> 巡目 </summary>
        public int jun = 1;

        /// <summary> 副露的面子 </summary>
        public List<MenOrJantou> fuuro = new List<MenOrJantou>();

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

        /// <summary> 一发 </summary>
        public bool ippatsu = false;

        /// <summary> 门清 </summary>
        public bool menzen = true;

        /// <summary> 听牌列表，无赤宝牌。不听牌时返回null </summary>
        public Tiles Tenpai {
            get {
                if (game.patternResolver.ResolveShanten(this, null, out var tiles, 0) == 0) {
                    return tiles;
                }
                return null;
            }
        }

        /// <summary> 是否振听 </summary>
        public bool IsFuriten {
            get {
                // TODO: 选择一个实现：
                // 1. 在所有玩家操作以后再将牌加入弃牌列表
                // 2. 在玩家打出牌时即将其加入弃牌表，但是在判定振听时忽略
                var tenpai = Tenpai;
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
        public int Count => fuuro.Select(gr => Math.Min(3, gr.Count)).Sum() + freeTiles.Count;

        public GameTile FindTile(Tile tile) => freeTiles.Find(t => t.tile == tile);
        public GameTiles FindTiles(Tiles tiles) {
            var tmp = new Tiles(tiles);
            var ret = new GameTiles();
            foreach (var tile in freeTiles) {
                if (tmp.Contains(tile.tile)) {
                    ret.Add(tile);
                    tmp.Remove(tile.tile);
                }
            }
            Debug.Assert(ret.Count == tiles.Count);
            return ret;
        }

        public void Add(GameTile tile) {
            tile.player = player;
            tile.source = TileSource.Hand;
            freeTiles.Add(tile);
        }

        public void Play(GameTile tile, bool riichi = false) {
            tile.player = null;
            tile.fromPlayer = player;
            tile.source = TileSource.Discard;
            freeTiles.Remove(tile);
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
        }

        public void Remove(GameTile tile) {
            if (freeTiles.Contains(tile)) {
                freeTiles.Remove(tile);
            }
        }

        public void AddGroup(MenOrJantou tiles, TileSource source) {
            fuuro.Add(tiles);
            tiles.ForEach(tile => {
                tile.player = player;
                tile.source = source;
                tile.riichi = false;
                Remove(tile);
            });
        }

        public void AddChi(Shun tiles) {
            AddGroup(tiles, TileSource.Chi);
        }

        public void AddPon(Kou tiles) {
            AddGroup(tiles, TileSource.Pon);
        }

        public void AddKan(Kan tiles) {
            AddGroup(tiles, tiles.IsClose ? TileSource.AnKan : TileSource.MinKan);
        }

        public void KaKan(Kan tiles) {
            tiles.IsKakan = true;
            var original = fuuro.Find(gr => gr is Kou && (gr.Contains(tiles[0]) || gr.Contains(tiles[1]))) as Kou;
            Debug.Assert(original != null, "加杠了个空气");
            fuuro.Remove(original);
            AddGroup(tiles, TileSource.KaKan);
        }
    }
}
