using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 国士无双十三面 : StdPattern {
        public 国士无双十三面(Base13_1 base13_1, 国士无双 国士无双) {
            BaseOn(base13_1);
            DependOn(国士无双);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool flag = hand.freeTiles.Select(tile => tile.tile.Val).Any(val => val == incoming.tile.Val);
            if (flag) {
                scores.Remove(dependOnPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 2, this));
            }
            return flag;
        }
    }
}
