using HoshinoSharp.Hoshino.Message;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Resolver {
    public class PlayerAction {
        /// <summary> 选项，全小写 </summary>
        public List<string> options;
        /// <summary> 显示给用户的消息 </summary>
        public HMessage msg;
        /// <summary> 参数是触发动作的options下标 </summary>
        public Action<int> trigger;
    }

    public class PlayerActions: List<PlayerAction> {
        public string GetMessage() {
            return string.Join("\n", this.Select(action => action.msg.ToString()));
        }
        public bool OnMessage(string msg) {
            msg = msg.Trim().ToLower();
            foreach (var action in this) {
                int index = action.options.FindIndex((option) => option.ToLower() == msg);
                if (index >= 0) {
                    action.trigger(index);
                    return true;
                }
            }
            return false;
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
