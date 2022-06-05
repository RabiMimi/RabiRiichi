using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class Chinroutou : StdPattern {
        public Chinroutou(Base33332 base33332, Honroutou honroutou, JunchanTaiyao junchanTaiyao) {
            BaseOn(base33332);
            DependOn(honroutou, junchanTaiyao);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            scores.Remove(afterPatterns);
            scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            return true;
        }
    }
}
