using HoshinoSharp.Hoshino.Message;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 判定是否能吃
    /// </summary>
    public class ChiResolver : ResolverBase {
        private IEnumerable<string> GenerateChi(string suffix = "") {
            if (!string.IsNullOrEmpty(suffix) && suffix.StartsWith(" ")) {
                GenerateChi(" " + suffix);
            }
            yield return "chi" + suffix;
            yield return "c" + suffix;
            yield return "吃" + suffix;
        }

        public override bool ResolveAction(Hand hand, GameTile incoming, out PlayerActions output) {
            if (hand.riichi || incoming.IsTsumo || incoming.tile.IsZ || incoming.fromPlayer != hand.PrevPlayer) {
                return Reject(out output);
            }
            int num = incoming.tile.Num;
            var current = new List<GameTile> { incoming };
            var result = new List<GameTiles>();
            CheckCombo(hand.hand, result, current, incoming.tile.Prev.Prev, incoming.tile.Prev);
            CheckCombo(hand.hand, result, current, incoming.tile.Prev, incoming.tile.Next);
            CheckCombo(hand.hand, result, current, incoming.tile.Next, incoming.tile.Next.Next);
            if (result.Count == 0) {
                return Reject(out output);
            }
            output = new PlayerActions();
            for (int i = 0; i < result.Count; i++) {
                var res = result[i];
                var str = result.Count <= 1 ? "" : (i + 1).ToString();
                output.Add(new PlayerAction {
                    priority = PlayerAction.Priority.CHI,
                    player = hand.player,
                    options = GenerateChi(str).ToList(),
                    msg = new HMessage($"c{str}：吃{res}"),
                    trigger = (_) => {
                        // TODO(Frenqy)
                    }
                });
            }
            return true;
        }
    }
}
