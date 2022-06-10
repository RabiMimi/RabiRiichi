using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class Chantaiyao : StdPattern {
        public Chantaiyao(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (groups.All(gr => gr.Any(tile => tile.tile.Is19Z))) {
                scores.Add(new Scoring(ScoringType.Han, hand.menzen ? 2 : 1, this));
                return true;
            }
            return false;
        }
    }
}
