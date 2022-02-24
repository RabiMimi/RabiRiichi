using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RabiRiichi.Riichi;

namespace RabiRiichi.Pattern {
    public class 岭上开花 : StdPattern {
        public override Type[] basePatterns => AllBasePatterns;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (incoming.source == TileSource.Wanpai) {
                scorings.Add(new Scoring(ScoringType.Han, 1, this));
                return true;
            }
            return false;
        }
    }
}