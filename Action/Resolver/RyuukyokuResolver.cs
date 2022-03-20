using RabiRiichi.Event.InGame;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;


namespace RabiRiichi.Action.Resolver {
    public class RyuukyokuResolver : ResolverBase {
        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            if (!player.game.IsFirstJun) {
                return false;
            }
            if (player.hand.freeTiles.All(t => t.tile.Is19Z) && incoming.tile.Is19Z) {
                output.Add(new RyuukyokuAction(player.id, new KyuushuKyuuhai(player.game.initialEvent)));
                return true;
            }
            return false;
        }

        protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile incoming) {
            yield return player;
        }
    }
}