using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 清一色 : StdPattern {
        public 清一色(AllExcept13_1 allExcept13_1, 混一色 混一色) {
            BaseOn(allExcept13_1);
            DependOn(混一色);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool 清一色 = !groups.SelectMany(gr => gr).Any(tile => tile.tile.IsZ);
            if (清一色) {
                scores.Remove(dependOnPatterns);
                scores.Add(new Scoring(ScoringType.Han, hand.menzen ? 6 : 5, this));
                return true;
            }
            return false;
        }
    }
}
