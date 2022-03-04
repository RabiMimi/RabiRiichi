using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 一气通贯 : StdPattern {
        public override Type[] basePatterns => Only33332;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            var isIttsuu = groups
                .Where(gr => gr is not Jantou)
                .OrderBy(gr => gr[0])
                .Subset(3)
                .Any(grs => grs.All((gr, index)
                    => gr is Shun
                    && gr.Suit == grs.First().Suit
                    && gr[0].tile.Num == index * 3 + 1));

            if (isIttsuu) {
                scorings.Add(new Scoring(ScoringType.Han, hand.menzen ? 2 : 1, this));
                return true;
            }
            return false;
        }
    }
}