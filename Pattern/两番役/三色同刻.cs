using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RabiRiichi.Pattern {
    public class 三色同刻 : StdPattern {
        public override Type[] basePatterns => Only33332;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
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