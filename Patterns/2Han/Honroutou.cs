using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class Honroutou : StdPattern {
        public Honroutou(AllBasePatterns allBasePatterns, Chantaiyao chantaiyao) {
            BaseOn(allBasePatterns);
            /// 混全不判定七对子，因此混老头不依赖于混全带幺九
            After(chantaiyao);
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
