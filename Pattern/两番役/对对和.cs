using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RabiRiichi.Pattern {
    public class 对对和 : StdPattern {
        public override Type[] basePatterns => Only33332;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (!groups.Any(gr => gr is Shun)) {
                scorings.Add(new Scoring(ScoringType.Han, 2, this));
                return true;
            }
            return false;
        }
    }
}
