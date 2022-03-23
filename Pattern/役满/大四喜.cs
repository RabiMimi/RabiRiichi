using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 大四喜 : StdPattern {
        public 大四喜(Base33332 base33332, 小四喜 小四喜) {
            BaseOn(base33332);
            DependOn(小四喜);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool flag = groups.Where(gr => gr is not Jantou && gr.First.tile.IsWind).Count() == 4;
            if (flag) {
                scores.Remove(dependOnPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            }
            return flag;
        }
    }
}
