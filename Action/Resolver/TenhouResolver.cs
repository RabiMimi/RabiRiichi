using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action.Resolver {
    /// <summary>
    /// 判定是否可以天和
    /// </summary>
    public class TenhouResolver : ResolverBase {
        protected readonly PatternResolver patternResolver;

        public TenhouResolver(PatternResolver patternResolver) {
            this.patternResolver = patternResolver;
        }

        protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile incoming) {
            yield return player;
        }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            // 此时incoming一定为null
            var freeTiles = player.hand.freeTiles;
            ScoreStorage maxScore = null;
            for (int i = 0; i < freeTiles.Count; i++) {
                incoming = freeTiles[i];
                freeTiles.RemoveAt(i);
                var score = patternResolver.ResolveMaxScore(player.hand, incoming, false);
                if (maxScore == null || score > maxScore) {
                    maxScore = score;
                }
                freeTiles.Insert(i, incoming);
            }
            if (maxScore != null && maxScore.cachedResult.IsValid(player.game.config.minHan)) {
                output.Add(new TsumoAction(player.id, maxScore, player.hand.freeTiles[^1]));
                return true;
            }
            return false;
        }
    }
}
