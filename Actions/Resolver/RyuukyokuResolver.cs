using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events.InGame;
using RabiRiichi.Util;
using System.Collections.Generic;
using System.Linq;


namespace RabiRiichi.Actions.Resolver {
    public class RyuukyokuResolver : ResolverBase {
        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            if (!player.game.config.ryuukyokuTrigger.HasAnyFlag(RyuukyokuTrigger.KyuushuKyuuhai) || !player.game.IsFirstJun) {
                return false;
            }
            bool flag = player.hand.freeTiles
                .Append(incoming)
                .Select(t => t.tile)
                .Distinct()
                .Count(t => t.Is19Z) >= 9;
            if (flag) {
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