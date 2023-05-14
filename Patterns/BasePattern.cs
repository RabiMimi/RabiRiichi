using RabiRiichi.Core;
using RabiRiichi.Utils;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public abstract class BasePattern {
        /// <summary>
        /// 判定计算时应计算舍牌还是进张。
        /// </summary>
        /// <returns>是否应当计算舍牌。若为 false，则应计算进张。</returns>
        public static bool ShouldComputeDiscard(Hand hand, GameTile incoming) {
            if (incoming == null) {
                Logger.Assert(hand.Count == Game.HAND_SIZE || hand.Count == Game.HAND_SIZE + 1,
                    $"Invalid: {hand.Count} tiles in hand and incoming is null");
                return hand.Count == Game.HAND_SIZE + 1;
            } else {
                Logger.Assert(hand.Count == Game.HAND_SIZE,
                    $"Invalid: {hand.Count} tiles in hand and incoming is not null");
                return true;
            }
        }

        /// <summary>
        /// 将手牌和进张合并，并按值分组，忽略红宝牌
        /// </summary>
        public static GameTileBucket GetTileGroups(Hand hand, GameTile incoming, bool includeGroups) {
            var tiles = (includeGroups
                ? hand.freeTiles.Concat(hand.called.SelectMany(gr => gr))
                : hand.freeTiles).ToList();
            if (incoming != null) {
                tiles.Add(incoming);
            }
            return new GameTileBucket(tiles);
        }

        /// <summary>
        /// 计算是否和牌，输出所有和牌的组合
        /// </summary>
        public abstract bool Resolve(Hand hand, GameTile incoming, out List<List<MenLike>> output);

        /// <summary>
        /// 计算向听数
        /// </summary>
        /// <param name="incoming">进张</param>
        /// <param name="output">
        /// 若总牌数为 13，输出有效进张列表
        /// 若总牌数为 14，输出打哪些牌向听数最少
        /// </param>
        /// <param name="maxShanten">
        /// 最大向听数，超过该数目将会返回int.MaxValue，较小数目可能能加快算法速度
        /// </param>
        /// <returns>向听数，-1为和</returns>
        public abstract int Shanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten = int.MaxValue);

        public static Tiles GetHand(List<GameTile> tiles, GameTile incoming, bool keepDora = false) {
            var ret = tiles.ToTiles();
            if (incoming != null) {
                ret.Add(incoming.tile);
            }
            if (!keepDora) {
                ret.ForEach(t => t.Akadora = false);
            }
            return ret;
        }

        protected static int Reject(out Tiles output) {
            output = null;
            return int.MaxValue;
        }
    }
}
