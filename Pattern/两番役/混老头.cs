using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 混老头 : StdPattern {
        public 混老头() {
            basePatterns = AllBasePatterns;
            /// 混全不判定七对子，因此混老头不依赖于混全带幺九
            afterPatterns = new Type[] {
                typeof(混全带幺九)
            };
        }

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (groups.All(gr => gr is not Shun && gr[0].tile.Is19Z)) {
                scorings.Remove(afterPatterns);
                scorings.Add(new Scoring(ScoringType.Han, 2, this));
                return true;
            }
            return false;
        }
    }
}
