using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
    public class KokushiMusou : StdPattern {
        public KokushiMusou(Base13_1 base13_1) {
            BaseOn(base13_1);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            return true;
        }
    }
}
