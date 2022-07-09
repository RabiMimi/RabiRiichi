using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Patterns;
using RabiRiichi.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions.Resolver {
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
            var config = player.game.config;
            if (config.riichiPolicy.HasAnyFlag(RiichiPolicy.SufficientTiles)
                && hand.game.wall.NumRemaining < config.playerCount) {
                return false;
            }
            if (player.points < config.MinRiichiPoints) {
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
            var handRiichiTiles = hand.freeTiles.Append(incoming).Where(t => riichiTiles.Contains(t.tile.WithoutDora)).ToList();
            if (handRiichiTiles.Count > 0) {
                output.Add(new RiichiAction(player.id, handRiichiTiles, incoming));
                return true;
            }
            return false;
        }
    }
}
