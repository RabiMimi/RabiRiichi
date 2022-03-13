using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 断幺九 : StdPattern {
        public 断幺九(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }
        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, Scorings scorings) {
            // TODO: 食断
            if (groups.SelectMany(gr => gr).Any(tile => tile.tile.Is19Z))
                return false;
            scorings.Add(new Scoring(ScoringType.Han, 1, this));
            return true;
        }
    }
}
