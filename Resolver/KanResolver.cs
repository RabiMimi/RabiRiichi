using RabiRiichi.Event;
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

        private PlayerAction GenerateAction(Hand hand, GameTile incoming, PlayerActions output, GameTiles group, string desc, bool onlyOne, bool close) {
            string str = onlyOne ? "" : (output.Count + 1).ToString(); 
            var ret = new PlayerAction {
                priority = PlayerAction.Priority.KAN,
                player = hand.player,
                options = GenerateKan(str).ToList(),
                trigger = (_) => {
                    /*
                    hand.game.eventBus.Queue(new GetTileEvent {
                        game = hand.game,
                        source = TileSource.Kan,
                        incoming = incoming,
                        player = hand.player,
                        group = group,
                    });
                    hand.game.eventBus.Queue(new DrawTileEvent {
                        game = hand.game,
                        type = close ? DrawTileType.CloseRinshan : DrawTileType.OpenRinshan,
                        player = hand.player,
                    });
                    */
                }
            };
            output.Add(ret);
            return ret;
        }

        public override bool ResolveAction(Hand hand, GameTile incoming, out PlayerActions output) {
            if (hand.game.wall.IsFinished) {
                return Reject(out output);
            }
            if (hand.player == incoming.fromPlayer) {
                // 自己打出来的
                return Reject(out output);
            }
            var tile = incoming.tile.WithoutDora;
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
                    GenerateAction(hand, incoming, output, gr, "加杠", tot <= 1, true);
                }
            }
            foreach (var gr in result) {
                GenerateAction(hand, incoming, output, gr, "杠", tot <= 1, incoming.IsTsumo);
            }
            if (output.Count == 0) {
                return Reject(out output);
            }
            return true;
        }
    }
}
