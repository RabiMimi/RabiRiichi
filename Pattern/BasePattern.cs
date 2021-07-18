using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public abstract class BasePattern {
        public static GameTiles[] GetTileGroups(Hand hand, GameTile incoming, bool includeGroups) {
            var tileGroups = new GameTiles[128];
            for (int i = 0; i < tileGroups.Length; i++) {
                tileGroups[i] = new GameTiles();
            }
            var tiles = (includeGroups
                ? hand.hand.Concat(hand.groups.SelectMany(gr => gr))
                : hand.hand).ToList();
            if (incoming != null) {
                tiles.Add(incoming);
            }
            foreach (var tile in tiles) {
                int index = tile.tile.NoDoraVal;
                tileGroups[index].Add(tile);
            }
            return tileGroups;
        }

        /// <summary>
        /// 计算是否和牌，输出所有和牌的组合
        /// </summary>
        public abstract bool Resolve(Hand hand, GameTile incoming, out List<List<GameTiles>> output);

        /// <summary>
        /// 计算向听数
        /// </summary>
        /// <param name="incoming">进张</param>
        /// <param name="output">
        /// 若incoming为null，输出有效进张列表
        /// 否则，输出打哪些牌向听数最少
        /// </param>
        /// <param name="maxShanten">
        /// 最大向听数，超过该数目将会返回int.MaxValue，较小数目可能能加快算法速度
        /// </param>
        /// <returns>向听数，-1为和</returns>
        public abstract int Shanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten = int.MaxValue);

        protected Tiles GetHand(GameTiles tiles, GameTile incoming, bool keepDora = false) {
            var ret = tiles.ToTiles();
            if (incoming != null) {
                ret.Add(incoming.tile);
            }
            if (!keepDora) {
                ret.ForEach(t => t.Akadora = false);
            }
            return ret;
        }

        protected int Reject(out Tiles output) {
            output = null;
            return int.MaxValue;
        }
    }
}
