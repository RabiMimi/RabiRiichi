using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 两杯口 : StdPattern {
        public 两杯口(Base33332 base33332, 一杯口 一杯口) {
            BaseOn(base33332);
            DependOn(一杯口);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (!hand.menzen)
                return false;

            var grs = groups
                .Where(tiles => tiles is Shun)
                .GroupBy(gr => gr.Value);
            bool isValid = grs.Count() == 2 && grs.All(gr => gr.Count() == 2);

            if (isValid) {
                scorings.Remove(dependOnPatterns);
                scorings.Add(new Scoring(ScoringType.Han, 3, this));
                return true;
            }
            return false;
        }
    }
}
