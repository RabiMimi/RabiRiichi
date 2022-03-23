using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class 清老头 : StdPattern {
        public 清老头(Base33332 base33332, 混老头 混老头, 纯全带幺九 纯全带幺九) {
            BaseOn(base33332);
            DependOn(混老头, 纯全带幺九);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            scores.Remove(dependOnPatterns);
            scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            return true;
        }
    }
}
