using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Daisuushii : StdPattern {
        public Daisuushii(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            var windGroups = groups.Where(gr => gr.First.tile.IsWind);
            bool flag = windGroups.All(gr => gr is not Jantou)
                && windGroups.GroupBy(gr => gr.First.tile).Count() == 4;
            if (flag) {
                scores.Add(new Scoring(ScoringType.Yakuman, 2, this));
                return true;
            }
            return false;
        }
    }
}
