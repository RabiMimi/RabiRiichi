using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class 海底摸月 : StdPattern {
        public 海底摸月(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }
        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (incoming.IsTsumo && hand.game.wall.IsHaitei) {
                scorings.Add(new Scoring(ScoringType.Han, 1, this));
                return true;
            }
            return false;
        }
    }
}