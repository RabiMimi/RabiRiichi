using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Suukantsu : StdPattern {
        public Suukantsu(Base33332 base33332, Sankantsu sankantsu, Toitoi toitoi) {
            BaseOn(base33332);
            DependOn(sankantsu, toitoi);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (groups.OfType<Kan>().Count() == 4) {
                scores.Remove(dependOnPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
                return true;
            }
            return false;
        }
    }
}
