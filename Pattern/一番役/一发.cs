using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class 一发 : StdPattern {
        public 一发(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (hand.ippatsu) {
                scorings.Add(new Scoring(ScoringType.Han, 1, this));
                return true;
            }
            return false;
        }
    }
}