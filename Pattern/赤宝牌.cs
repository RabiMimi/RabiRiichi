using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 赤宝牌 : BonusPattern {
        public override Type[] dependOnPatterns => AllBasePatterns;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            int count = groups.SelectMany(tile => tile.ToTiles()).Count(tile => tile.Akadora);
            if (count > 0) {
                scorings.Add(new Scoring(ScoringType.Han, count, this));
                return true;
            }
            return false;
        }
    }
}
