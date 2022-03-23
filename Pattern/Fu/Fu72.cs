using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class Fu72 : StdPattern {
        public override string name => "fu";

        public Fu72(Base72 base72) {
            BaseOn(base72);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            scores.Add(new Scoring(ScoringType.Fu, 25, this));
            return true;
        }
    }
}
