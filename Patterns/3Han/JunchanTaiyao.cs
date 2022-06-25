using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class JunchanTaiyao : StdPattern {
        public JunchanTaiyao(Base33332 base33332, Chantaiyao chantaiyao) {
            BaseOn(base33332);
            DependOn(chantaiyao);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (!groups.SelectMany(gr => gr).Any(tile => tile.tile.IsZ)) {
                scores.Remove(afterPatterns);
                scores.Add(new Scoring(ScoringType.Han, hand.menzen ? 3 : 2, this));
                return true;
            }
            return false;
        }
    }
}
