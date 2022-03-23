using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 小四喜 : StdPattern {
        public 小四喜(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool flag = groups.Where(gr => gr.First.tile.IsWind).Count() == 4;
            if (flag) {
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            }
            return true;
        }
    }
}
