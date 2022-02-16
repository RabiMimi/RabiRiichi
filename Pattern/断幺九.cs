using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RabiRiichi.Riichi;

namespace RabiRiichi.Pattern {
    public class 断幺九 : StdPattern {
        public override Type[] dependOnPatterns => AllBasePatterns;

        public override bool Resolve(List<GameTiles> groups, Hand hand, GameTile incoming, Scorings scorings) {
            // TODO: 食断
            if (groups.SelectMany(gr => gr).Any(tile => tile.tile.Is19Z))
                return false;
            scorings.Add(new Scoring(ScoringType.Han, 1, this));
            return true;
        }
    }
}
