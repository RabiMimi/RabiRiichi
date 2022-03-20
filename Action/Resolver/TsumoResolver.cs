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
            // 不需要判定振听，因为这里已经保证是自摸了
            var maxScore = patternResolver.ResolveMaxScore(hand, incoming, false);
            if (maxScore != null && maxScore.cachedResult.IsValid(player.game.config.minHan)) {
                output.Add(new TsumoAction(hand.player.id, maxScore, incoming), true);
                return true;
            }
            return false;
        }
    }
}
