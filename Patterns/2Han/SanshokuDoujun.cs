using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class SanshokuDoujun : StdPattern {
        public SanshokuDoujun(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool isSanshokuDoujun = groups
                .Where(gr => gr is not Jantou)
                .OrderBy(gr => gr.First)
                .Subset(3)
                .Any(grs => grs.All((gr, index)
                    => gr is Shun
                    && (int)gr.Suit == index + 1
                    && gr.First.tile.Num == grs.First().First.tile.Num));
            if (isSanshokuDoujun) {
                scores.Add(new Scoring(ScoringType.Han, hand.menzen ? 2 : 1, this));
            }
            return isSanshokuDoujun;
        }
    }
}