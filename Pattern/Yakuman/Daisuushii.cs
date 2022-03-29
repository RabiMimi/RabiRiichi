using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Daisuushii : StdPattern {
        public Daisuushii(Base33332 base33332, Shousuushii shousuushii) {
            BaseOn(base33332);
            DependOn(shousuushii);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool flag = groups.Where(gr => gr is not Jantou && gr.First.tile.IsWind).Count() == 4;
            if (flag) {
                scores.Remove(dependOnPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 2, this));
                return true;
            }
            return false;
        }
    }
}
