using RabiRiichi.Action;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action.Resolver {
    /// <summary>
    /// 判定是否可以自摸
    /// </summary>
    public class TsumoResolver : ResolverBase {

        private readonly PatternResolver patternResolver;

        public TsumoResolver(PatternResolver patternResolver) {
            this.patternResolver = patternResolver;
        }

        protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile incoming) {
            yield return player;
        }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            var hand = player.hand;
            if (hand.IsFuriten && !incoming.IsTsumo) {
                return false;
            }
            var maxScore = patternResolver.ResolveMaxScore(hand, incoming, false);
            if (maxScore != null && maxScore.IsValid(player.game.config.minHan)) {
                output.Add(new TsumoAction(hand.player.id));
                return true;
            }
            return false;
        }
    }
}
