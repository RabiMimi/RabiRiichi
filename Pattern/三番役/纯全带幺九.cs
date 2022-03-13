using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 纯全带幺九 : StdPattern {
        public 纯全带幺九(Base33332 base33332, 混全带幺九 混全带幺九) {
            BaseOn(base33332);
            DependOn(混全带幺九);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (!groups.SelectMany(gr => gr).Any(tile => tile.tile.IsZ)) {
                scorings.Remove(dependOnPatterns);
                scorings.Add(new Scoring(ScoringType.Han, hand.menzen ? 3 : 2, this));
                return true;
            }
            return false;
        }
    }
}
