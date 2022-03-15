using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 混老头 : StdPattern {
        public 混老头(AllBasePatterns allBasePatterns, 混全带幺九 混全带幺九) {
            BaseOn(allBasePatterns);
            /// 混全不判定七对子，因此混老头不依赖于混全带幺九
            After(混全带幺九);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (groups.All(gr => gr is not Shun && gr.First.tile.Is19Z)) {
                scores.Remove(afterPatterns);
                scores.Add(new Scoring(ScoringType.Han, 2, this));
                return true;
            }
            return false;
        }
    }
}
