using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class KokushiMusouJuusanmenMachi : StdPattern {
        public KokushiMusouJuusanmenMachi(Base13_1 base13_1, KokushiMusou kokushiMusou) {
            BaseOn(base13_1);
            DependOn(kokushiMusou);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool flag = hand.freeTiles.Select(tile => tile.tile).Any(tile => tile.IsSame(incoming.tile));
            if (flag) {
                scores.Remove(afterPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 2, this));
            }
            return flag;
        }
    }
}
