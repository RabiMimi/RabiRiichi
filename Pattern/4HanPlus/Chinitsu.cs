using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Chinitsu : StdPattern {
        public Chinitsu(AllExcept13_1 allExcept13_1, Honitsu honitsu) {
            BaseOn(allExcept13_1);
            DependOn(honitsu);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool Chinitsu = !groups.SelectMany(gr => gr).Any(tile => tile.tile.IsZ);
            if (Chinitsu) {
                scores.Remove(afterPatterns);
                scores.Add(new Scoring(ScoringType.Han, hand.menzen ? 6 : 5, this));
                return true;
            }
            return false;
        }
    }
}
