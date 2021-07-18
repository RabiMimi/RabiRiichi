using HoshinoSharp.Hoshino.Message;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 判定是否能碰
    /// </summary>
    public class PonResolver : ResolverBase {

        private IEnumerable<string> GeneratePon(string suffix = "") {
            if (!string.IsNullOrEmpty(suffix) && suffix.StartsWith(" ")) {
                GeneratePon(" " + suffix);
            }
            yield return "pon" + suffix;
            yield return "p" + suffix;
            yield return "碰" + suffix;
        }

        public override bool ResolveAction(Hand hand, GameTile incoming, out PlayerActions output) {
            if (hand.riichi || incoming.IsTsumo) {
                return Reject(out output);
            }
            var tile = incoming.tile.WithoutDora;
            var current = new List<GameTile> { incoming };
            var result = new List<GameTiles>();
            CheckCombo(hand.hand, result, current, tile, tile);
            if (result.Count == 0) {
                return Reject(out output);
            }
            output = new PlayerActions();
            for (int i = 0; i < result.Count; i++) {
                var res = result[i];
                var str = result.Count <= 1 ? "" : (i + 1).ToString();
                output.Add(new PlayerAction {
                    priority = PlayerAction.Priority.PON,
                    player = hand.player,
                    options = GeneratePon(str).ToList(),
                    msg = new HMessage($"p{str}：碰{res}"),
                    trigger = (_) => {
                        // TODO(Frenqy)
                    }
                });
            }
            return true;
        }
    }
}
