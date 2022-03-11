using RabiRiichi.Action;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 判定是否可以立直
    /// </summary>
    public class RiichiResolver : ResolverBase {
        private readonly PatternResolver patternResolver;
        public RiichiResolver(PatternResolver patternResolver) {
            this.patternResolver = patternResolver;
        }

        protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile incoming) {
            yield return player;
        }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            var hand = player.hand;
            if (hand.game.wall.NumRemaining < hand.game.config.playerCount) {
                return false;
            }
            if (hand.riichi || !hand.menzen || incoming == null || !incoming.IsTsumo) {
                return false;
            }
            int shanten = patternResolver.ResolveShanten(hand, incoming, out var riichiTiles, 0);
            if (shanten >= 1) {
                return false;
            }
            if (shanten == -1) {
                riichiTiles.AddRange(BasePattern.GetHand(hand.freeTiles, incoming).Distinct());
            }
            var handRiichiTiles = hand.freeTiles.Where(t => riichiTiles.Contains(t.tile.WithoutDora)).ToList();
            output.Add(new RiichiAction(player, handRiichiTiles));
            return true;
        }
    }
}
