using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 三杠子 : StdPattern {
        public 三杠子(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (groups.Where(gr => gr is Kan).Count() >= 3) {
                scores.Add(new Scoring(ScoringType.Han, 2, this));
                return true;
            }
            return false;
        }
    }
}
