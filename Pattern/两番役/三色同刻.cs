using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 三色同刻 : StdPattern {
        public 三色同刻(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, Scorings scorings) {
            bool isSanshoku = groups
                .Where(gr => gr is not Jantou)
                .OrderBy(gr => gr[0])
                .Subset(3)
                .Any(grs => grs.All((gr, index)
                    => gr is Kan or Kou
                    && (int)gr.Suit == index + 1
                    && gr[0].tile.Num == grs.First()[0].tile.Num));
            // TODO: (Frenqy) Finish this
            return false;
        }
    }
}