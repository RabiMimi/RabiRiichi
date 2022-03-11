using RabiRiichi.Action;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Action.Resolver {
    /// <summary>
    /// 判定是否能杠
    /// </summary>
    public class KanResolver : ResolverBase {
        protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile _) {
            return player.game.players.Where(p => !p.SamePlayer(player));
        }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            var hand = player.hand;
            if (player.game.wall.IsHaitei) {
                return false;
            }
            var tile = incoming.tile.WithoutDora;
            var current = new List<GameTile> { incoming };
            var result = new List<GameTiles>();
            CheckCombo(hand.freeTiles, result, current, tile, tile, tile);
            if (result.Count == 0) {
                return false;
            }
            output.Add(new KanAction(player, result, -incoming.fromPlayer.Dist(player)));
            return true;
        }
    }
}
