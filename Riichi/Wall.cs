using RabiRiichi.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Riichi {
    public class Wall {
        public Game game;

        /// <summary> 宝牌数量 </summary>
        public const int NUM_DORA = 5;
        /// <summary> 岭上牌数量 </summary>
        public const int NUM_RINSHAN = 4;
        /// <summary> 牌山剩下的牌 </summary>
        public readonly ListStack<Tile> remaining = new(Tiles.All);
        /// <summary> 岭上牌 </summary>
        public readonly ListStack<Tile> rinshan = new();
        /// <summary> 宝牌 </summary>
        public readonly ListStack<Tile> doras = new();
        /// <summary> 里宝牌 </summary>
        public readonly ListStack<Tile> uradoras = new();
        /// <summary> 已翻的Dora数量 </summary>
        public int revealedDoraCount = 0;
        /// <summary> 玩家还尚不知道的牌，用于搞事 </summary>
        public IEnumerable<Tile> hiddenTiles =>
            remaining
            .Concat(rinshan)
            .Concat(doras.Skip(revealedDoraCount))
            .Concat(uradoras);
        /// <summary> 牌山剩下的牌数 </summary>
        public int NumRemaining => remaining.Count;
        /// <summary> 是否到了海底 </summary>
        public bool IsHaitei => NumRemaining <= 0;

        public Wall(Rand rand) {
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
        public Tile Draw() => remaining.Pop();

        /// <summary> 抽若干张牌 </summary>
        public IEnumerable<Tile> Draw(int count) => remaining.PopMany(count);

        /// <summary> 翻一张宝牌 </summary>
        public Tile RevealDora() => doras[revealedDoraCount++];


        /// <summary> 计算tile算几番宝牌（不考虑里宝牌/红宝牌）。非宝牌返回0 </summary>
        public int CountDora(Tile tile)
            => doras.Count(dora => dora.NextDora.IsSame(tile));

        /// <summary> 计算tile中几番里宝牌。非里宝牌返回0 </summary>
        public int CountUradora(Tile tile)
            => uradoras.Count(uradora => uradora.NextDora.IsSame(tile));

        /// <summary> 抽一张岭上牌 </summary>
        public Tile DrawRinshan() => rinshan.Pop();

        /// <summary>
        /// 去掉一张玩家未知牌（需要在<see cref="hiddenTiles"/>中）
        /// 若该牌在王牌里，则牌山最后一张牌会补充王牌
        /// </summary>
        public bool Remove(Tile tile) {
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
        private static bool Swap(ListStack<Tile> target, int targetIndex, ListStack<Tile> searchFrom, Tile tile) {
            int index = searchFrom.IndexOf(tile);
            if (index < -1)
                return false;
            (target[targetIndex], searchFrom[index]) = (searchFrom[index], target[targetIndex]);
            return true;
        }

        /// <summary>
        /// 将tile与target的targetIndex位置的tile进行交换
        /// </summary>
        private void Swap(ListStack<Tile> target, int targetIndex, Tile tile) {
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

        /// <summary> 将一张牌作为牌山第i前的牌（从0开始） </summary>
        public void Insert(int i, Tile tile) {
            Remove(tile);
            remaining.Insert(remaining.Count - i, tile);
        }

        /// <summary> 将一张牌放到牌山最前 </summary>
        public void InsertFirst(Tile tile) {
            Insert(0, tile);
        }

        /// <summary> 将一张牌放到牌山最后 </summary>
        public void InsertLast(Tile tile) {
            Insert(remaining.Count - 1, tile);
        }

        /// <summary> 将一张牌作为第i张里宝牌 </summary>
        public void PlaceUradora(int i, Tile tile) {
            Swap(uradoras, i, tile);
        }

        /// <summary> 将一张牌作为第i张宝牌 </summary>
        public void PlaceDora(int i, Tile tile) {
            Swap(doras, i, tile);
        }

        /// <summary> 将一张牌放到岭上牌最前 </summary>
        public void PlaceRinshanFirst(Tile tile) {
            Swap(rinshan, rinshan.Count - 1, tile);
        }

        /// <summary> 将一张牌放到岭上牌最后 </summary>
        public void PlaceRinshanLast(Tile tile) {
            Swap(rinshan, 0, tile);
        }
    }
}
