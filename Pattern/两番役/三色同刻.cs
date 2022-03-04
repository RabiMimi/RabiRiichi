using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RabiRiichi.Pattern {
    public class 三色同刻 : StdPattern {
        public override Type[] basePatterns => Only33332;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            var grs = groups.Where(gr => gr is not Jantou).ToList();
            for (int i = 0; i < grs.Count; i++) {
                var checkList = grs.Where((gr, index) => index != i);
            }
            return false;
        }
    }
}