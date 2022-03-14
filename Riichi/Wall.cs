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

        public readonly Tiles drawn = new();
        public readonly Tiles remaining = Tiles.All;
        public readonly Stack<Tile> rinshan = new();
        public readonly Tiles doras = new();
        public readonly Tiles uradoras = new();

        public int NumRemaining => remaining.Count
            + doras.Count + uradoras.Count - NUM_DORA * 2
            - (NUM_RINSHAN - rinshan.Count);
        /// <summary> 是否到了海底 </summary>
        public bool IsHaitei => NumRemaining <= 0;

        private readonly Rand rand;

        public Wall(Rand rand) {
            this.rand = rand;
            var tiles = Select(NUM_RINSHAN);
            tiles.ForEach(t => rinshan.Push(t));
            remaining.Remove(rinshan);
        }

        /// <summary> 检查牌山是否还有给定的牌数 </summary>
        public bool Has(int amount) {
            return NumRemaining >= amount;
        }

        public bool Draw(Tile tile) {
            if (!remaining.Contains(tile)) {
                return false;
            }
            remaining.Remove(tile);
            drawn.Add(tile);
            return true;
        }

        public void RevealDora(Tile tile, Tile uraDora) {
            doras.Add(tile);
            uradoras.Add(uraDora);
            remaining.Remove(tile);
            remaining.Remove(uraDora);
        }

        public bool Draw(IEnumerable<Tile> tiles) {
            foreach (var tile in tiles) {
                if (!Draw(tile))
                    return false;
            }
            return true;
        }

        /// <summary> 计算tile算几番宝牌（不考虑里宝牌/红宝牌）。非宝牌返回0 </summary>
        public int CountDora(Tile tile) {
            return doras.Count(dora => dora.NextDora.IsSame(tile));
        }

        /// <summary> 计算tile中几番里宝牌。非里宝牌返回0 </summary>
        public int CountUradora(Tile tile) {
            return uradoras.Count(uradora => uradora.NextDora.IsSame(tile));
        }

        /// <summary> 随机从牌山中选择一张牌 </summary>
        public Tile SelectOne() {
            return rand.Choice(remaining);
        }

        public Tiles Select(int count) {
            return new Tiles(rand.Choice(remaining, count));
        }

        public Tile NextRinshan => rinshan.Peek();
        /// <summary> 抽一张牌，然后去掉一张岭上牌 </summary>
        public void DrawRinshan(Tile tile) {
            int index = remaining.IndexOf(tile);
            if (index < 0) {
                // 不在牌山里，则必定抽岭上牌
                if (tile != NextRinshan) {
                    throw new ArgumentException($"{tile} is not in the wall or rinshan");
                }
                rinshan.Pop();
                return;
            }
            // 在牌山里，有人搞事，假装这张是岭上牌
            remaining.RemoveAt(index);
            remaining.Add(NextRinshan);
            rinshan.Pop();
        }
    }
}
