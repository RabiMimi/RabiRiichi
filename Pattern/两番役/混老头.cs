using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RabiRiichi.Pattern {
    public class 混老头 : StdPattern {
        public override Type[] basePatterns => AllBasePatterns;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (groups.All(gr => gr is not Shun && gr[0].tile.Is19Z)) {
                scorings.Add(new Scoring(ScoringType.Han, 2, this));
                return true;
            }
            return false;
        }
    }
}
