using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 四杠子 : StdPattern {
        public 四杠子(Base33332 base33332, 三杠子 三杠子, 对对和 对对和) {
            BaseOn(base33332);
            DependOn(三杠子, 对对和);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (groups.Where(gr => gr is Kan).Count() == 4) {
                scores.Remove(dependOnPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
                return true;
            }
            return false;
        }
    }
}
