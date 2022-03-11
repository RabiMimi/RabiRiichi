using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 四杠子 : StdPattern {
        public 四杠子() {
            basePatterns = Only33332;
            dependOnPatterns = new Type[] {
                typeof(三杠子),
                typeof(对对和)
            };
        }
        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (groups.Where(gr => gr is Kan).Count() == 4) {
                scorings.Remove(dependOnPatterns);
                scorings.Add(new Scoring(ScoringType.Yakuman, 1, this));
                return true;
            }
            return false;
        }
    }
}
