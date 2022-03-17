using RabiRiichi.Action;
using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action.Resolver {
    /// <summary>
    /// 判定是否能吃
    /// </summary>
    public class ChiResolver : ResolverBase {
        protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile incoming) {
            yield return player.PrevPlayer;
        }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            var hand = player.hand;
            if (player.game.wall.IsHaitei) {
                return false;
            }
            if (hand.riichi || incoming.IsTsumo || incoming.tile.IsZ) {
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
            output.Add(new ChiAction(player.id, result, -incoming.discardInfo!.fromPlayer.Dist(player)));
            return true;
        }
    }
}
