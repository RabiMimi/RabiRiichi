using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 混一色 : StdPattern {
        public 混一色(AllExcept13_1 allExcept13_1) {
            BaseOn(allExcept13_1);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool 混一色 = groups
                .SelectMany(gr => gr)
                .Where(tile => tile.tile.IsMPS)
                .GroupBy(tile => tile.tile.Suit)
                .Count() == 1;
            if (混一色) {
                scores.Add(new Scoring(ScoringType.Han, hand.menzen ? 3 : 2, this));
                return true;
            }
            return false;
        }
    }
}
