using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 混全带幺九 : StdPattern {
        public override Type[] basePatterns => Only33332;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (groups.All(gr => gr.Any(tile => tile.tile.Is19Z))) {
                scorings.Add(new Scoring(ScoringType.Han, hand.menzen ? 2 : 1, this));
                return true;
            }
            return false;
        }
    }
}
