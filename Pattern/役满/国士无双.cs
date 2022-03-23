using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class 国士无双 : StdPattern {
        public 国士无双(Base13_1 base13_1) {
            BaseOn(base13_1);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            return true;
        }
    }
}
