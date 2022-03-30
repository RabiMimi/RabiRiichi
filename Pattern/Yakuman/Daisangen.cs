using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Daisangen : StdPattern {
        public Daisangen(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            var sangenGroups = groups.Where(gr => gr.First.tile.IsSangen);
            bool flag = sangenGroups.All(gr => gr is not Jantou)
                && sangenGroups.GroupBy(gr => gr.First.tile).Count() == 3;
            if (flag) {
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
                return true;
            }
            return false;
        }
    }
}
