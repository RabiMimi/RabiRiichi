using RabiRiichi.Riichi;
using RabiRiichi.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Resolver {
    public class PlayerAction {
        public enum Priority {
            NONE,
            SKIP,
            PLAY,
            CHI,
            PON,
            KAN,
            RIICHI,
            RON,
        }
        /// <summary> 优先级，最高优先级的操作才会触发 </summary>
        public Priority priority;
        /// <summary> 需要操作的用户 </summary>
        public Player player;
        /// <summary> 选项，全小写 </summary>
        public List<string> options;
        /// <summary> 触发动作的options下标 </summary>
        public int choice = -1;
        /// <summary> 参数是this </summary>
        public Action<PlayerAction> trigger;
    }

    public class PlayerActions: List<PlayerAction> {
        private readonly List<PlayerAction> confirmed = new List<PlayerAction>();
        public bool Finished { get; private set; } = false;

        public async Task Wait() {
            while (!Finished) {
                await Task.Yield();
            }
        }

        /// <summary>
        /// 监听消息
        /// </summary>
        /// <returns>是否移除该监听器</returns>
        public async Task<bool> OnMessage(Game game, int player) {
            return true;
            // TODO: Fix this
            /*
            var actions = this.Where(action => action.player == player).ToArray();
            if (actions.Length == 0) {
                return false;
            }
            bool suc = false;
            foreach (var action in actions) {
                int index = action.options.FindIndex((option) => true);
                if (index >= 0) {
                    action.choice = index;
                    confirmed.Add(action);
                    suc = true;
                    break;
                }
            }
            if (suc) {
                foreach (var action in actions) {
                    Remove(action);
                }
                var maxConfirmed = confirmed
                    .Select(action => action.priority)
                    .DefaultIfEmpty(PlayerAction.Priority.NONE)
                    .Max();
                var maxUnconfirmed = this
                    .Select(action => action.priority)
                    .DefaultIfEmpty(PlayerAction.Priority.NONE)
                    .Max();
                if (maxConfirmed > maxUnconfirmed -
                    (maxConfirmed != PlayerAction.Priority.RON).ToInt()) {
                    foreach (var action in confirmed
                        .Where(action => action.priority == maxConfirmed)) {
                        action.trigger(action);
                    }
                    return true;
                }
            }
            return false;*/
        }
    }

    public abstract class ResolverBase {
        public abstract bool ResolveAction(Hand hand, GameTile incoming, out PlayerActions output);

        private static bool CheckComboDfs(
            List<GameTile> current,
            List<GameTiles> output,
            List<GameTile> hand, int handIndex,
            List<Tile> tiles, int tileIndex) {
            if (tileIndex >= tiles.Count) {
                var toAdd = new GameTiles(current);
                toAdd.Sort();
                var str = tiles.ToString();
                if (output.All(t => t.ToString() != toAdd.ToString())) {
                    output.Add(toAdd);
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
                    (i > handIndex && cur.tile == hand[i-1].tile)) {
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
        public static bool CheckCombo(List<GameTile> hand, List<GameTiles> output, List<GameTile> current, params Tile[] tiles) {
            var tileList = tiles.ToList();
            tileList.Sort();
            hand.Sort();
            return CheckComboDfs(current, output, hand, 0, tileList, 0);
        }

        public static bool Reject<T>(out T output) where T: class {
            output = null;
            return false;
        }
    }
}
