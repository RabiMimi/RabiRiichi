using RabiRiichi.Pattern;
using RabiRiichi.Action;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 判定是否可以和牌
    /// </summary>
    public class RonResolver : ResolverBase {
        /// <summary> 番缚 </summary>
        public int MinHan { get; set; } = 1;

        private PatternResolver patternResolver;

        public RonResolver(PatternResolver patternResolver) {
            this.patternResolver = patternResolver;
        }

        public override bool ResolveAction(Hand hand, GameTile incoming, MultiPlayerAction output) {
            if (hand.IsFuriten && !incoming.IsTsumo) {
                return false;
            }
            if (hand.player == incoming.fromPlayer) {
                // 自己打出来的
                return false;
            }
            var maxScore = patternResolver.ResolveMaxScore(hand, incoming, false);
            if (maxScore != null && maxScore.IsValid(MinHan)) {
                output.Add(new RonAction(hand.player));
                return true;
            }
            return false;
        }
    }
}
