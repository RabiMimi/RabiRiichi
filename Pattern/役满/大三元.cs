using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 大三元 : StdPattern {
        public 大三元(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool flag = groups.Where(gr => gr is not Jantou && gr.First.tile.IsSangen).Count() == 3;
            if (flag) {
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            }
            return flag;
        }
    }
}
