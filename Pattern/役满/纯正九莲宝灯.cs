using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 纯正九莲宝灯 : StdPattern {
        public 纯正九莲宝灯(Base33332 base33332, 九莲宝灯 九莲宝灯) {
            BaseOn(base33332);
            DependOn(九莲宝灯);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            var grs = hand.freeTiles.OrderBy(tile => tile).GroupBy(tile => tile.tile.Num);
            bool flag = grs.Count() == 9 && grs.First().Count() == 3 && grs.Last().Count() == 3;
            if (flag) {
                scores.Remove(dependOnPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 2, this));
            }
            return flag;
        }
    }
}
