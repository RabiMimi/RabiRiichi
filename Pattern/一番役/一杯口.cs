using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 一杯口 : StdPattern {
        public override Type[] basePatterns => Only33332;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (!hand.menzen)
                return false;

            bool isValid = groups
                .Where(tiles => tiles is Shun)
                .Subset(2)
                .Any(grs => grs.First().IsSame(grs.Last()));

            if (isValid) {
                scorings.Add(new Scoring(ScoringType.Han, 1, this));
                return true;
            }
            return false;
        }
    }
}
