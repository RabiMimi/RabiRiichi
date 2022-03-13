using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 四暗刻 : StdPattern {
        public 四暗刻(Base33332 base33332, 三暗刻 三暗刻, 对对和 对对和) {
            BaseOn(base33332);
            DependOn(三暗刻, 对对和);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (groups.Where(gr => gr.IsClose).Count() == 4) {
                scorings.Remove(dependOnPatterns);
                scorings.Add(new Scoring(ScoringType.Yakuman, 1, this));
                return true;
            }
            return false;
        }
    }
}
