using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
    public class Fu13_1 : StdPattern {
        public override string name => "fu";

        public Fu13_1(Base13_1 base13_1) {
            BaseOn(base13_1);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            scores.Add(new Scoring(ScoringType.Fu, 25, this));
            return true;
        }
    }
}
