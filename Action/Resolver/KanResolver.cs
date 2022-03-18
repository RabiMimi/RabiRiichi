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
            if (tile.IsTsumo) {
                // 暗杠或加杠
                yield return player;
            } else {
                // 来自别的玩家的牌，大明杠
                foreach (var p in player.game.players.Where(p => !p.SamePlayer(player)))
                    yield return p;
            }
        }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            if (player.game.wall.IsHaitei) {
                return false;
            }
            var hand = player.hand;
            if (!incoming.IsTsumo && hand.riichi) {
                return false;
            }
            var tile = incoming.tile.WithoutDora;
            var current = new List<GameTile> { incoming };
            var result = new List<GameTiles>();

            // 大明杠或暗杠
            CheckCombo(hand.freeTiles, result, current, tile, tile, tile);
            // 加杠
            if (incoming.IsTsumo) {
                var groups = hand.called.Where(g => g is Kou && g.First.tile.IsSame(incoming.tile));
                foreach (var group in groups) {
                    current.AddRange(new GameTiles(group.Append(incoming)));
                }
            }

            if (result.Count == 0) {
                return false;
            }
            output.Add(new KanAction(player.id, result, incoming, -incoming.discardInfo!.fromPlayer.Dist(player)));
            return true;
        }
    }
}
