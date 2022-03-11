using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 纯全带幺九 : StdPattern {
        public 纯全带幺九() {
            basePatterns = Only33332;
            dependOnPatterns = new Type[] { typeof(混全带幺九) };
        }

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (!groups.SelectMany(gr => gr).Any(tile => tile.tile.IsZ)) {
                scorings.Remove(dependOnPatterns);
                scorings.Add(new Scoring(ScoringType.Han, hand.menzen ? 3 : 2, this));
                return true;
            }
            return false;
        }
    }
}
