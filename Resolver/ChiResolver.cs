using RabiRiichi.Action;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 判定是否能吃
    /// </summary>
    public class ChiResolver : ResolverBase {
        public override bool ResolveAction(Hand hand, GameTile incoming, MultiPlayerAction output) {
            if (hand.game.wall.IsFinished) {
                return false;
            }
            if (hand.riichi || incoming.IsTsumo || incoming.tile.IsZ || incoming.fromPlayer != hand.player.PrevPlayer) {
                return false;
            }
            var current = new List<GameTile> { incoming };
            var result = new List<GameTiles>();
            CheckCombo(hand.freeTiles, result, current, incoming.tile.Prev.Prev, incoming.tile.Prev);
            CheckCombo(hand.freeTiles, result, current, incoming.tile.Prev, incoming.tile.Next);
            CheckCombo(hand.freeTiles, result, current, incoming.tile.Next, incoming.tile.Next.Next);
            if (result.Count == 0) {
                return false;
            }
            output.Add(new ChiAction(hand.player, result));
            return true;
        }
    }
}
