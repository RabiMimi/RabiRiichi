using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 九莲宝灯 : StdPattern {
        public 九莲宝灯(Base33332 base33332, 清一色 清一色) {
            BaseOn(base33332);
            DependOn(清一色);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            var grs = groups.SelectMany(gr => gr).GroupBy(tile => tile.tile.Num);
            bool flag = grs.Count() == 9 && grs.First().Count() >= 3 && grs.Last().Count() >= 3;
            if (flag) {
                scores.Remove(dependOnPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            }
            return flag;
        }
    }
}
