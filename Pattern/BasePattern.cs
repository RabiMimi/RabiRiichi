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
        /// <param name="incoming">
        /// 若incoming为null，输出可以让向听数-1的进张<br/>
        /// 若incoming不为null，输出打出哪些牌可以让向听数-1
        /// </param>
        /// <returns>向听数</returns>
        //public abstract int Shanten(Hand hand, GameTile incoming, out List<GameTiles> output);
    }
}
