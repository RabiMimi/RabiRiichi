using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class Ittsu : StdPattern {
        public Ittsu(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            var isIttsu = groups
                .Where(gr => gr is not Jantou)
                .OrderBy(gr => gr.First)
                .Subset(3)
                .Any(grs => grs.All((gr, index)
                    => gr is Shun
                    && gr.Suit == grs.First().Suit
                    && gr.First.tile.Num == index * 3 + 1));

            if (isIttsu) {
                scores.Add(new Scoring(ScoringType.Han, hand.menzen ? 2 : 1, this));
                return true;
            }
            return false;
        }
    }
}