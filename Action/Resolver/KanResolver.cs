using RabiRiichi.Action;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Action.Resolver {
    /// <summary>
    /// 判定是否能杠
    /// </summary>
    public class KanResolver : ResolverBase {
        protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile tile) {
            if (tile.fromPlayer != null) {
                // 来自玩家的牌，明杠
                foreach (var p in player.game.players.Where(p => !p.SamePlayer(player)))
                    yield return p;
            } else {
                // 暗杠
                yield return player;
            }
        }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            // TODO: 加杠
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
            output.Add(new KanAction(player.id, result, -incoming.fromPlayer.Dist(player)));
            return true;
        }
    }
}
