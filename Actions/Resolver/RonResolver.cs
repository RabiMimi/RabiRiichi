using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Patterns;
using RabiRiichi.Util;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions.Resolver {
    /// <summary>
    /// 判定是否可以和牌
    /// </summary>
    public class RonResolver : ResolverBase {
        protected readonly PatternResolver patternResolver;

        public RonResolver(PatternResolver patternResolver) {
            this.patternResolver = patternResolver;
        }

        protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile incoming) {
            return player.game.players.Where(p => !p.SamePlayer(player));
        }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            var hand = player.hand;
            if (hand.isFuriten) {
                return false;
            }
            var maxScore = patternResolver.ResolveMaxScore(hand, incoming, PatternMask.All);
            if (maxScore != null && maxScore.result.IsValid(player.game.config.minHan)) {
                output.Add(new RonAction(hand.player.id, maxScore, incoming,
                    player.game.config.agariOption.HasAnyFlag(AgariOption.FirstWinner)
                        ? -incoming.discardInfo.fromPlayer.Dist(player) : 0), true);
                return true;
            }
            return false;
        }
    }
}
