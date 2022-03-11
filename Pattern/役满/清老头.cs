using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class 清老头 : StdPattern {
        public 清老头() {
            basePatterns = Only33332;
            dependOnPatterns = new Type[] {
                typeof(混老头),
                typeof(纯全带幺九)
            };
        }

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            scorings.Remove(dependOnPatterns);
            scorings.Add(new Scoring(ScoringType.Yakuman, 1, this));
            return true;
        }
    }
}
