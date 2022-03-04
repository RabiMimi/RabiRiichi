using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RabiRiichi.Pattern {
    public class 双立直 : StdPattern {
        public override Type[] basePatterns => Only33332;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            // TODO: 双立直
            return false;
        }
    }
}
