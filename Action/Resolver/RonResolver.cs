using RabiRiichi.Action;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Action.Resolver {
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
            if (hand.IsFuriten) {
                return false;
            }
            var maxScore = patternResolver.ResolveMaxScore(hand, incoming, false);
            if (maxScore != null && maxScore.IsValid(player.game.config.minHan)) {
                output.Add(new RonAction(hand.player.id));
                return true;
            }
            return false;
        }
    }
}
