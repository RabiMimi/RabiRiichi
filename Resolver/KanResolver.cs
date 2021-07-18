using HoshinoSharp.Hoshino.Message;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 判定是否能杠
    /// </summary>
    public class KanResolver : ResolverBase {

        private IEnumerable<string> GenerateKan(string suffix = "") {
            if (!string.IsNullOrEmpty(suffix) && suffix.StartsWith(" ")) {
                GenerateKan(" " + suffix);
            }
            yield return "kan" + suffix;
            yield return "k" + suffix;
            yield return "杠" + suffix;
        }

        private PlayerAction GenerateAction(PlayerActions output, GameTiles group, string desc, bool onlyOne) {
            string str = onlyOne ? "" : (output.Count + 1).ToString(); 
            var ret = new PlayerAction {
                options = GenerateKan(str).ToList(),
                msg = new HMessage($"k{str}：{desc}{group}"),
                trigger = (_) => {
                    // TODO(Frenqy)
                }
            };
            output.Add(ret);
            return ret;
        }

        public override bool ResolveAction(Hand hand, GameTile incoming, out PlayerActions output) {
            var tile = incoming.tile.NoDora;
            // 暗杠/明杠
            var current = new List<GameTile> { incoming };
            var result = new List<GameTiles>();
            CheckCombo(hand.hand, result, current, tile, tile, tile);
            output = new PlayerActions();
            int tot = result.Count;
            if (incoming.IsTsumo) {
                // 加杠
                var resultExtra = hand.groups.Where(
                    gr => gr.IsKou && gr[0].IsSame(incoming)).ToList();
                tot += resultExtra.Count;
                foreach (var gr in resultExtra) {
                    GenerateAction(output, gr, "加杠", tot <= 1);
                }
            }
            foreach (var gr in result) {
                GenerateAction(output, gr, "杠", tot <= 1);
            }
            return true;
        }
    }
}
