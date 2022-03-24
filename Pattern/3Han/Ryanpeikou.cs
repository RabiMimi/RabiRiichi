using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Ryanpeikou : StdPattern {
        public Ryanpeikou(Base33332 base33332, Iipeikou iipeikou) {
            BaseOn(base33332);
            DependOn(iipeikou);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (!hand.menzen)
                return false;

            var grs = groups
                .Where(tiles => tiles is Shun)
                .GroupBy(gr => gr.Value);
            bool isValid = grs.Count() == 2 && grs.All(gr => gr.Count() == 2);

            if (isValid) {
                scores.Remove(dependOnPatterns);
                scores.Add(new Scoring(ScoringType.Han, 3, this));
                return true;
            }
            return false;
        }
    }
}