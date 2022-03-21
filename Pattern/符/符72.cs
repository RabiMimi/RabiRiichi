using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class 符72 : StdPattern {
        public 符72(Base72 base72) {
            BaseOn(base72);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            scores.Add(new Scoring(ScoringType.Fu, 25, this));
            return true;
        }
    }
}
