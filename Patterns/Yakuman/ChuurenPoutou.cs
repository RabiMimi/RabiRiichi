using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class ChuurenPoutou : StdPattern {
        public ChuurenPoutou(Base33332 base33332, Chinitsu chinitsu) {
            BaseOn(base33332);
            DependOn(chinitsu);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (!hand.menzen || groups.Any(gr => gr is Kan)) {
                return false;
            }

            var grs = groups.SelectMany(gr => gr).OrderBy(tile => tile).GroupBy(tile => tile.tile.Num);
            bool flag = grs.Count() == 9 && grs.First().Count() >= 3 && grs.Last().Count() >= 3;
            if (flag) {
                scores.Remove(afterPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            }
            return flag;
        }
    }
}
