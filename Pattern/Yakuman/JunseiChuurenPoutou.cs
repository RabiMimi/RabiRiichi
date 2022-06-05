using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class JunseiChuurenPoutou : StdPattern {
        public JunseiChuurenPoutou(Base33332 base33332, ChuurenPoutou chuurenPoutou) {
            BaseOn(base33332);
            DependOn(chuurenPoutou);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            var grs = hand.freeTiles.OrderBy(tile => tile).GroupBy(tile => tile.tile.Num);
            bool flag = grs.Count() == 9 && grs.First().Count() == 3 && grs.Last().Count() == 3;
            if (flag) {
                scores.Remove(afterPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 2, this));
                return true;
            }
            return false;
        }
    }
}
