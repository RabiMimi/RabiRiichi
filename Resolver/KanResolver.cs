using RabiRiichi.Action;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 判定是否能杠
    /// </summary>
    public class KanResolver : ResolverBase {

        public override bool ResolveAction(Hand hand, GameTile incoming, MultiPlayerInquiry output) {
            if (hand.game.wall.IsHaitei) {
                return false;
            }
            if (hand.player == incoming.fromPlayer) {
                // 自己打出来的
                return false;
            }
            var tile = incoming.tile.WithoutDora;
            var current = new List<GameTile> { incoming };
            var result = new List<GameTiles>();
            CheckCombo(hand.freeTiles, result, current, tile, tile, tile);
            if (result.Count == 0) {
                return false;
            }
            output.Add(new KanAction(hand.player, result));
            return true;
        }
    }
}
