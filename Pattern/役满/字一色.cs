using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 字一色 : StdPattern {
        public 字一色(AllExcept13_1 allExcept13_1) {
            BaseOn(allExcept13_1);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (groups.SelectMany(gr => gr).All(tile => tile.tile.IsZ)) {
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
                return true;
            }
            return false;
        }
    }
}
