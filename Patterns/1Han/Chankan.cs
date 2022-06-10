using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
    public class Chankan : StdPattern {
        public override PatternMask type => PatternMask.Luck;

        public Chankan(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }
        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (incoming.discardInfo?.reason == DiscardReason.ChanKan) {
                scores.Add(new Scoring(ScoringType.Han, 1, this));
                return true;
            }
            return false;
        }
    }
}