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
            var freeTiles = player.hand.freeTiles;
            ScoreStorage maxScore = null;
            freeTiles.Add(incoming);
            for (int i = 0; i < freeTiles.Count; i++) {
                var tile = freeTiles[i];
                freeTiles.RemoveAt(i);
                var score = patternResolver.ResolveMaxScore(player.hand, tile, false);
                if (score != null && (maxScore == null || score > maxScore)) {
                    maxScore = score;
                }
                freeTiles.Insert(i, tile);
            }
            freeTiles.Remove(incoming);
            if (maxScore != null && maxScore.cachedResult.IsValid(player.game.config.minHan)) {
                output.Add(new TsumoAction(player.id, maxScore, incoming));
                return true;
            }
            return false;
        }
    }
}
