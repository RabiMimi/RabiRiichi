using RabiRiichi.Core.Config;
using RabiRiichi.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Core {
    public class Wall {
        public readonly GameConfig config;
        public readonly RabiRand rand;

        /// <summary> 宝牌数量 </summary>
        public const int NUM_DORA = 5;
        /// <summary> 岭上牌数量 </summary>
        public const int NUM_RINSHAN = 4;
        /// <summary> 牌山剩下的牌 </summary>
        public readonly ListStack<GameTile> remaining = new();
        /// <summary> 岭上牌 </summary>
        public readonly ListStack<GameTile> rinshan = new();
        /// <summary> 宝牌 </summary>
        public readonly ListStack<GameTile> doras = new();
        /// <summary> 里宝牌 </summary>
        public readonly ListStack<GameTile> uradoras = new();
        /// <summary> 已翻的Dora数量 </summary>
        public int revealedDoraCount = 0;
        /// <summary> 已翻的里Dora数量 </summary>
        public int revealedUradoraCount = 0;
        /// <summary> 玩家还尚不知道的牌，用于搞事 </summary>
        public IEnumerable<GameTile> hiddenTiles =>
            remaining
            .Concat(rinshan)
            .Concat(doras.Skip(revealedDoraCount))
            .Concat(uradoras);
        /// <summary> 牌山剩下的牌数 </summary>
        public int NumRemaining => remaining.Count - (NUM_RINSHAN - rinshan.Count);
        /// <summary> 是否到了海底 </summary>
        public bool IsHaitei => NumRemaining <= 0;

        public Wall(RabiRand rand, GameConfig config) {
            this.rand = rand;
            this.config = config;
        }

        /// <summary> 重置牌山 </summary>
        public void Reset() {
            remaining.Clear();
            rinshan.Clear();
            doras.Clear();
            uradoras.Clear();
            remaining.AddRange(Tiles.All.ToGameTiles());
            rand.Shuffle(remaining);
            rinshan.AddRange(remaining.PopMany(NUM_RINSHAN));
            doras.AddRange(remaining.PopMany(NUM_DORA));
            uradoras.AddRange(remaining.PopMany(NUM_DORA));
        }

        /// <summary> 检查牌山是否还有给定的牌数 </summary>
        public bool Has(int amount) {
            return NumRemaining >= amount;
        }


        /// <summary> 抽一张牌 </summary>
        public GameTile Draw() {
            var ret = remaining.Pop();
            ret.source = TileSource.Wall;
            return ret;
        }

        /// <summary> 抽若干张牌 </summary>
        public List<GameTile> Draw(int count) {
            var ret = remaining.PopMany(count).ToList();
            foreach (var tile in ret) {
                tile.source = TileSource.Wall;
            }
            return ret;
        }

        /// <summary> 翻一张宝牌 </summary>
        public GameTile RevealDora(bool isKan) {
            bool revealUradora = config.doraOption.HasAnyFlag(
                isKan ? DoraOption.KanUradora : DoraOption.InitialUradora);
            bool revealDora = config.doraOption.HasAnyFlag(
                isKan ? DoraOption.KanDora : DoraOption.InitialDora);
            if (revealUradora && revealedUradoraCount < uradoras.Count) {
                revealedUradoraCount++;
            }
            if (revealDora && revealedDoraCount < doras.Count) {
                var ret = doras[revealedDoraCount++];
                ret.source = TileSource.Wanpai;
                return ret;
            }
            return null;
        }

        /// <summary> 计算tile算几番宝牌（不考虑里宝牌/红宝牌）。非宝牌返回0 </summary>
        public int CountDora(Tile tile)
            => doras.Take(revealedDoraCount).Count(dora => dora.tile.NextDora.IsSame(tile));

        /// <summary> 计算tile中几番里宝牌。非里宝牌返回0 </summary>
        public int CountUradora(Tile tile)
            => uradoras.Take(revealedUradoraCount).Count(uradora => uradora.tile.NextDora.IsSame(tile));

        /// <summary> 抽一张岭上牌 </summary>
        public GameTile DrawRinshan() {
            var ret = rinshan.Pop();
            ret.source = TileSource.Wanpai;
            return ret;
        }

        /// <summary>
        /// 去掉一张玩家未知牌（需要在<see cref="hiddenTiles"/>中）
        /// 若该牌在王牌里，则牌山最后一张牌会补充王牌
        /// </summary>
        public bool Remove(GameTile tile) {
            if (remaining.Remove(tile))
                return true;
            if (IsHaitei)
                return false;
            var newTile = remaining[0];
            int index;
            if ((index = rinshan.IndexOf(tile)) >= 0) {
                remaining.RemoveAt(0);
                rinshan[index] = newTile;
                return true;
            }
            if ((index = uradoras.IndexOf(tile)) >= 0) {
                remaining.RemoveAt(0);
                uradoras[index] = newTile;
                return true;
            }
            if ((index = doras.IndexOf(tile)) >= revealedDoraCount) {
                remaining.RemoveAt(0);
                doras[index] = newTile;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 若tile在searchFrom中，将tile与target的targetIndex位置的tile进行交换
        /// </summary>
        private static bool Swap(ListStack<GameTile> target, int targetIndex, ListStack<GameTile> searchFrom, GameTile tile) {
            int index = searchFrom.IndexOf(tile);
            if (index < 0)
                return false;
            (target[targetIndex], searchFrom[index]) = (searchFrom[index], target[targetIndex]);
            return true;
        }

        /// <summary>
        /// 将tile与target的targetIndex位置的tile进行交换
        /// </summary>
        private void Swap(ListStack<GameTile> target, int targetIndex, GameTile tile) {
            if (Swap(target, targetIndex, remaining, tile))
                return;
            if (Swap(target, targetIndex, rinshan, tile))
                return;
            if (Swap(target, targetIndex, uradoras, tile))
                return;
            int index = doras.IndexOf(tile);
            if (index >= revealedDoraCount) {
                (doras[index], target[targetIndex]) = (target[targetIndex], doras[index]);
                return;
            }
            throw new ArgumentException("tile is already drawn or revealed, cannot swap");
        }

        /// <summary>
        /// 用tile替换target的targetIndex位置的牌
        /// </summary>
        /// <returns>被替换的牌</returns>
        private static GameTile Replace(ListStack<GameTile> target, int targetIndex, GameTile tile) {
            var ret = target[targetIndex];
            target[targetIndex] = tile;
            return ret;
        }

        /// <summary>
        /// 在hiddenTiles中寻找tile对应的牌
        /// </summary>
        public GameTile FindInHidden(Tile tile, bool ignoreAkadora = true) {
            return hiddenTiles.FirstOrDefault(t => ignoreAkadora ? t.tile.IsSame(tile) : t.tile == tile);
        }

        /// <summary> 将一张牌作为牌山第i前的牌（从0开始） </summary>
        public void Insert(int i, GameTile tile) {
            Remove(tile);
            remaining.Insert(remaining.Count - i, tile);
        }

        /// <summary> 用一张不在牌山里的牌替换牌山里第i张牌（从0开始） </summary>
        /// <returns>被替换的牌</returns>
        public GameTile Replace(int i, GameTile tile) {
            i = remaining.Count - i - 1;
            var ret = remaining[i];
            remaining[i] = tile;
            return ret;
        }

        /// <summary> 将一张牌放到牌山最前 </summary>
        public void InsertFirst(GameTile tile) {
            Insert(0, tile);
        }

        /// <summary> 用一张不在牌山里的牌替换牌山最前的牌 </summary>
        /// <returns>被替换的牌</returns>
        public GameTile ReplaceFirst(GameTile tile) {
            return Replace(0, tile);
        }

        /// <summary> 将一张牌放到牌山最后 </summary>
        public void InsertLast(GameTile tile) {
            if (remaining.Contains(tile)) {
                Insert(remaining.Count - 1, tile);
            } else {
                Insert(remaining.Count, tile);
            }
        }

        /// <summary> 用一张不在牌山里的牌替换牌山最后的牌 </summary>
        /// <returns>被替换的牌</returns>
        public GameTile ReplaceLast(GameTile tile) {
            return Replace(remaining.Count - 1, tile);
        }

        /// <summary> 将一张牌作为第i张里宝牌 </summary>
        public void PlaceUradora(int i, GameTile tile) {
            Swap(uradoras, i, tile);
        }

        /// <summary> 用一张不在牌山里的牌替换第i张里宝牌 </summary>
        /// <returns>被替换的牌</returns>
        public GameTile ReplaceUradora(int i, GameTile tile) {
            return Replace(uradoras, i, tile);
        }

        /// <summary> 将一张牌作为第i张宝牌 </summary>
        public void PlaceDora(int i, GameTile tile) {
            Swap(doras, i, tile);
        }

        /// <summary> 用一张不在牌山里的牌替换第i张宝牌 </summary>
        /// <returns>被替换的牌</returns>
        public GameTile ReplaceDora(int i, GameTile tile) {
            return Replace(doras, i, tile);
        }

        /// <summary> 将一张牌作为第i张岭上牌（从0开始） </summary>
        public void PlaceRinshan(int i, GameTile tile) {
            Swap(rinshan, rinshan.Count - i - 1, tile);
        }

        /// <summary> 用一张不在牌山里的牌替换第i张岭上牌（从0开始） </summary>
        /// <returns>被替换的牌</returns>
        public GameTile ReplaceRinshan(int i, GameTile tile) {
            return Replace(rinshan, rinshan.Count - i - 1, tile);
        }

        /// <summary> 将一张牌放到岭上牌最前 </summary>
        public void PlaceRinshanFirst(GameTile tile) {
            PlaceRinshan(0, tile);
        }

        /// <summary> 用一张不在牌山里的牌替换岭上牌最前的牌 </summary>
        /// <returns>被替换的牌</returns>
        public GameTile ReplaceRinshanFirst(GameTile tile) {
            return Replace(rinshan, rinshan.Count - 1, tile);
        }

        /// <summary> 将一张牌放到岭上牌最后 </summary>
        public void PlaceRinshanLast(GameTile tile) {
            PlaceRinshan(rinshan.Count - 1, tile);
        }

        /// <summary> 用一张不在牌山里的牌替换岭上牌最后的牌 </summary>
        /// <returns>被替换的牌</returns>
        public GameTile ReplaceRinshanLast(GameTile tile) {
            return Replace(rinshan, 0, tile);
        }
    }
}
