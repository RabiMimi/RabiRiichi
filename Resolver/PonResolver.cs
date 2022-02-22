using RabiRiichi.Action;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 判定是否能碰
    /// </summary>
    public class PonResolver : ResolverBase {
        public override bool ResolveAction(Hand hand, GameTile incoming, MultiPlayerAction output) {
            if (hand.game.wall.IsHaitei) {
                return false;
            }
            if (hand.riichi || incoming.IsTsumo || hand.player == incoming.fromPlayer) {
                return false;
            }
            var tile = incoming.tile.WithoutDora;
            var current = new List<GameTile> { incoming };
            var result = new List<GameTiles>();
            CheckCombo(hand.freeTiles, result, current, tile, tile);
            if (result.Count == 0) {
                return false;
            }
            output.Add(new PonAction(hand.player, result));
            return true;
        }
    }
}
