using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Riichi {
    public class Wall {
        public Game game;

        /// <summary>
        /// 王牌数量
        /// </summary>
        public const int NUM_WANPAI = 14;

        public Tiles drawn;
        public Tiles remaining;
        public Tiles doras;
        public Tiles uradoras;

        public int NumRemaining => remaining.Count + doras.Count + uradoras.Count - NUM_WANPAI;
        /// <summary> 是否到了海底 </summary>
        public bool IsHaitei => NumRemaining <= 0;

        public Wall(Game game, string tiles = "", string doras = "", string uradoras = "") {
            this.game = game;
            drawn = new Tiles(tiles);
            this.doras = new Tiles(doras);
            this.uradoras = new Tiles(uradoras);
            remaining = Tiles.All;
            remaining.Remove(drawn);
            remaining.Remove(this.doras);
            remaining.Remove(this.uradoras);
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

        /// <summary>
        /// 
        /// </summary>
        public Tile SelectOne() {
            return game.rand.Choice(remaining);
        }

        public Tiles Select(int count) {
            return new Tiles(game.rand.Choice(remaining, count));
        }
    }
}
