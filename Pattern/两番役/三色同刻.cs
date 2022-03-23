using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 三色同刻 : StdPattern {
        public 三色同刻(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool isSanshoku = groups
                .Where(gr => gr is not Jantou)
                .OrderBy(gr => gr.First)
                .Subset(3)
                .Any(grs => grs.All((gr, index)
                    => gr is Kan or Kou
                    && (int)gr.Suit == index + 1
                    && gr.First.tile.Num == grs.First().First.tile.Num));
            if (isSanshoku) {
                scores.Add(new Scoring(ScoringType.Han, 2, this));
            }
            return isSanshoku;
        }
    }
}