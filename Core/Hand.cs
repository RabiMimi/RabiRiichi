using RabiRiichi.Pattern;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RabiRiichi.Core {
    public class Hand {
        /// <summary> 手牌（不包含副露） </summary>
        public List<GameTile> freeTiles = new();

        /// <summary> 巡目 </summary>
        public int jun = 0;

        /// <summary> 鸣牌的面子 </summary>
        public List<MenLike> called = new();

        /// <summary> 牌河 </summary>
        public List<GameTile> discarded = new();

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

        /// <summary> 和了牌 </summary>
        public GameTile agariTile;
        /// <summary> 是否已经和了 </summary>
        public bool agari => agariTile != null;

        /// <summary> 听牌列表，无赤宝牌。不听牌时返回空List </summary>
        public Tiles Tenpai {
            get {
                if (game.Get<PatternResolver>().ResolveShanten(this, null, out var tiles, 0) == 0) {
                    return tiles;
                }
                return Tiles.Empty;
            }
        }

        /// <summary> 是否同巡振听 </summary>
        public bool isTempFuriten = false;
        /// <summary> 是否立直振听 </summary>
        public bool isRiichiFuriten = false;
        /// <summary> 是否摸切振听 </summary>
        public bool isDiscardFuriten = false;
        /// <summary> 是否振听 </summary>
        public bool isFuriten => isTempFuriten || isRiichiFuriten || isDiscardFuriten;

        /// <summary> 牌的总数，注意：杠会被算作3张牌 </summary>
        public int Count => called.Select(gr => Math.Min(3, gr.Count)).Sum() + freeTiles.Count;

        public GameTile FindTile(Tile tile) => freeTiles.Find(t => t.tile == tile);
        public IEnumerable<GameTile> FindTiles(Tiles tiles) {
            var tmp = new Tiles(tiles);
            foreach (var tile in freeTiles) {
                if (tmp.Contains(tile.tile)) {
                    tmp.Remove(tile.tile);
                    yield return tile;
                }
            }
        }

        public void Add(GameTile tile) {
            tile.player = player;
            tile.source = TileSource.Hand;
            freeTiles.Add(tile);
        }

        public void Riichi(GameTile tile, bool wRiichi) {
            riichiTile = tile;
            this.wRiichi = wRiichi;
            player.points -= game.config.riichiPoints;
            game.info.riichiStick++;
            riichiStick++;
        }

        public void Play(GameTile tile, DiscardReason reason) {
            tile.discardInfo = new DiscardInfo(player, reason);
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
            called.Add(tiles);
            foreach (var tile in tiles) {
                tile.player = player;
                tile.source = source;
                if (tile.formTime == -1) {
                    tile.formTime = game.info.timeStamp.Next;
                }
                Remove(tile);
            }
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
            var original = called.OfType<Kou>().FirstOrDefault(gr => gr.Contains(tiles[0]) || gr.Contains(tiles[1]));
            Debug.Assert(original != null, "加杠了个空气");
            called.Remove(original);
            AddGroup(tiles, TileSource.KaKan);
        }
    }
}
