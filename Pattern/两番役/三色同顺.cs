using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 三色同顺 : StdPattern {
        public 三色同顺(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool is三色同顺 = groups
                .Where(gr => gr is not Jantou)
                .OrderBy(gr => gr.First)
                .Subset(3)
                .Any(grs => grs.All((gr, index)
                    => gr is Shun
                    && (int)gr.Suit == index + 1
                    && gr.First.tile.Num == grs.First().First.tile.Num));
            if (is三色同顺) {
                scores.Add(new Scoring(ScoringType.Han, hand.menzen ? 2 : 1, this));
            }
            return is三色同顺;
        }
    }
}