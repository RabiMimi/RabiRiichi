using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class 双立直 : StdPattern {
        public 双立直(AllBasePatterns allBasePatterns, 立直 立直) {
            BaseOn(allBasePatterns);
            DependOn(立直);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (hand.wRiichi) {
                scores.Remove(dependOnPatterns);
                scores.Add(new Scoring(ScoringType.Han, 2, this));
                return true;
            }
            return false;
        }
    }
}
