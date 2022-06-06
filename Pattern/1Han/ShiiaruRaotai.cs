using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class ShiiaruRaotai : StdPattern {
        public override PatternMask type => PatternMask.Regular;

        public ShiiaruRaotai(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (hand.freeTiles.Count == 1) {
                scores.Add(new Scoring(ScoringType.Han, 1, this));
                return true;
            }
            return false;
        }
    }
}