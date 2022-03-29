using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Action.Resolver {

    public abstract class ResolverBase {

        /// <summary>
        /// 计算玩家可以执行的动作
        /// </summary>
        /// <param name="player">当前计算的玩家</param>
        /// <param name="incoming">摸到或打出的牌</param>
        protected abstract bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output);

        /// <summary>
        /// 计算哪些玩家可以执行该动作
        /// </summary>
        /// <param name="player">与该操作有关的玩家，例如打出该牌或摸到该牌</param>
        /// <param name="incoming">摸到或打出的牌</param>
        protected abstract IEnumerable<Player> ResolvePlayers(Player player, GameTile incoming);

        private static bool CheckComboDfs(
            List<GameTile> current,
            List<List<GameTile>> output,
            List<GameTile> hand, int handIndex,
            List<Tile> tiles, int tileIndex) {
            if (tileIndex >= tiles.Count) {
                if (output.All(t => !t.SequenceEqual(current))) {
                    output.Add(current.ToList());
                    return true;
                }
                return false;
            }
            var tile = tiles[tileIndex];
            if (!tile.IsValid) {
                return false;
            }
            bool success = false;
            for (int i = handIndex; i < hand.Count; i++) {
                var cur = hand[i];
                if (!cur.tile.IsSame(tile) ||
                    (i > handIndex && cur.tile == hand[i - 1].tile)) {
                    continue;
                }
                current.Add(cur);
                success |= CheckComboDfs(current, output, hand, i + 1, tiles, tileIndex + 1);
                current.RemoveAt(current.Count - 1);
            }
            return success;
        }

        /// <summary>
        /// 检查hand里是否有tiles对应的GameTile，并把所有合法组合和current结合后加入到output<br/>
        /// tiles对赤宝牌不敏感，output中普通版本和赤宝牌版本会被看作不同的组合
        /// </summary>
        /// <returns>是否至少有一个合法组合</returns>
        public static bool CheckCombo(List<GameTile> hand, List<List<GameTile>> output, List<GameTile> current, params Tile[] tiles) {
            var tileList = tiles.ToList();
            tileList.Sort();
            hand.Sort();
            return CheckComboDfs(current, output, hand, 0, tileList, 0);
        }

        public bool Resolve(Player current, GameTile incoming, MultiPlayerInquiry output) {
            var players = ResolvePlayers(current, incoming);
            bool success = false;
            foreach (var player in players) {
                success |= ResolveAction(player, incoming, output);
            }
            return success;
        }
    }
}
