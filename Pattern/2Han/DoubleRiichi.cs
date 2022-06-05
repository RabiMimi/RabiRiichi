using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class DoubleRiichi : StdPattern {
        public DoubleRiichi(AllBasePatterns allBasePatterns, Riichi riichi) {
            BaseOn(allBasePatterns);
            DependOn(riichi);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (hand.wRiichi) {
                scores.Remove(afterPatterns);
                scores.Add(new Scoring(ScoringType.Han, 2, this));
                return true;
            }
            return false;
        }
    }
}
