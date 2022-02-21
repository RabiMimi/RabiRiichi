using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabiRiichi.Pattern {
    public class 门清自摸和 : StdPattern {
        public override Type[] dependOnPatterns => AllBasePatterns;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (hand.menzen && incoming.IsTsumo) {
                scorings.Add(new Scoring(ScoringType.Han, 1, this));
                return true;
            }
            return false;
        }
    }
}
