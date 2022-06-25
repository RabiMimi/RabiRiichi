using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class Tsuuiisou : StdPattern {
        public Tsuuiisou(AllExcept13_1 allExcept13_1, Honroutou honroutou) {
            BaseOn(allExcept13_1);
            After(honroutou);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (groups.SelectMany(gr => gr).All(tile => tile.tile.IsZ)) {
                scores.Remove(afterPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
                return true;
            }
            return false;
        }
    }
}
