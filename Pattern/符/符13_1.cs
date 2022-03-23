using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class 符13_1 : StdPattern {
        public 符13_1(Base13_1 base13_1) {
            BaseOn(base13_1);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            scores.Add(new Scoring(ScoringType.Fu, 25, this));
            return true;
        }
    }
}
