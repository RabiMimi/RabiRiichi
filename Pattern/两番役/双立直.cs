using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class 双立直 : StdPattern {
        public 双立直(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (hand.wRiichi) {
                scores.Add(new Scoring(ScoringType.Han, 1, this));
            }
            return hand.wRiichi;
        }
    }
}
