using RabiRiichi.Pattern;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RabiRiichi.Riichi {
    public class Hand {
        /// <summary> 手牌（不包含副露） </summary>
        public GameTiles freeTiles = new();

        /// <summary> 巡目 </summary>
        public int jun = 0;

        /// <summary> 副露的面子 </summary>
        public List<MenLike> fuuro = new();

        /// <summary> 牌河 </summary>
        public GameTiles discarded = new();

        /// <summary> 当前玩家 </summary>
        public Player player;

        /// <summary> 当前游戏实例 </summary>
        public Game game => player.game;

        /// <summary> 第一个立直宣告牌 </summary>
        public GameTile riichiTile = null;

        /// <summary> 立直 </summary>
        public bool riichi => riichiTile != null;

        /// <summary> W立直 </summary>
        public bool wRiichi = false;

        /// <summary> 场上立直棒数量，不可用于判定是否立直 </summary>
        public int riichiStick = 0;

        /// <summary> 一发 </summary>
        public bool ippatsu = false;

        /// <summary> 门清 </summary>
        public bool menzen = true;

        /// <summary> 是否已经和了 </summary>
        public bool agari = false;

        /// <summary> 听牌列表，无赤宝牌。不听牌时返回null </summary>
        public Tiles Tenpai {
            get {
                if (game.Get<PatternResolver>().ResolveShanten(this, null, out var tiles, 0) == 0) {
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
                        .Where(tile => tile.discardInfo.discardTime >= riichiTile.discardInfo.discardTime)
                        .Any(tile => tenpai.Contains(tile.tile.WithoutDora));
                } else {
                    // 同巡振听
                    var discarded = game.AllDiscardedTiles.ToList();
                    int lastIndex = discarded.FindLastIndex(tile => tile.fromPlayerId == player.id);
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

        public void Riichi(GameTile tile, bool wRiichi) {
            riichiTile = tile;
            this.wRiichi = wRiichi;
            riichiStick++;
            ippatsu = true;
        }

        public void Play(GameTile tile, DiscardReason reason) {
            ippatsu = false;
            tile.discardInfo = new DiscardInfo(player, reason, game.info.timeStamp.Next);
            tile.source = TileSource.Discard;
            freeTiles.Remove(tile);
            discarded.Add(tile);
        }

        public void Remove(GameTile tile) {
            if (freeTiles.Contains(tile)) {
                freeTiles.Remove(tile);
            }
        }

        public void AddGroup(MenLike tiles, TileSource source) {
            fuuro.Add(tiles);
            tiles.ForEach(tile => {
                tile.player = player;
                tile.source = source;
                if (tile.formTime == -1) {
                    tile.formTime = game.info.timeStamp.Next;
                }
                Remove(tile);
            });
        }

        public void AddChii(Shun tiles) {
            AddGroup(tiles, TileSource.Chii);
        }

        public void AddPon(Kou tiles) {
            AddGroup(tiles, TileSource.Pon);
        }

        public void AddKan(Kan tiles) {
            AddGroup(tiles, tiles.IsClose ? TileSource.AnKan : TileSource.DaiMinKan);
        }

        public void KaKan(Kan tiles) {
            var original = fuuro.Find(gr => gr is Kou && (gr.Contains(tiles[0]) || gr.Contains(tiles[1]))) as Kou;
            Debug.Assert(original != null, "加杠了个空气");
            fuuro.Remove(original);
            AddGroup(tiles, TileSource.KaKan);
        }
    }
}
