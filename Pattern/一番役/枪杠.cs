using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class 枪杠 : StdPattern {
        public 枪杠(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }
        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (incoming.source == TileSource.KaKan) {
                scores.Add(new Scoring(ScoringType.Han, 1, this));
                return true;
            }
            return false;
        }
    }
}