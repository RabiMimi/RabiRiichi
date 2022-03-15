using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 混一色 : StdPattern {
        public 混一色(AllExecpt13_1 allExecpt13_1) {
            BaseOn(allExecpt13_1);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, Scorings scorings) {
            bool 混一色 = groups
                .SelectMany(gr => gr)
                .Where(tile => tile.tile.IsMPS)
                .GroupBy(tile => tile.tile.Suit)
                .Count() == 1;
            if (混一色) {
                scorings.Add(new Scoring(ScoringType.Han, hand.menzen ? 3 : 2, this));
                return true;
            }
            return false;
        }
    }
}
