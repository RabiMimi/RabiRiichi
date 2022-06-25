using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class Iipeikou : StdPattern {
        public Iipeikou(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (!hand.menzen)
                return false;

            bool isValid = groups
                .OfType<Shun>()
                .Subset(2)
                .Any(grs => grs.First().IsSame(grs.Last()));

            if (isValid) {
                scores.Add(new Scoring(ScoringType.Han, 1, this));
                return true;
            }
            return false;
        }
    }
}
