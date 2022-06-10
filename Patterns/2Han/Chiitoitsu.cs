using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
    public class Chiitoitsu : StdPattern {
        public Chiitoitsu(Base72 base72) {
            BaseOn(base72);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            scores.Add(new Scoring(ScoringType.Han, 2, this));
            return true;
        }
    }
}
