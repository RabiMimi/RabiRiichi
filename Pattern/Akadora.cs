using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Akadora : StdPattern {
        public override Type[] dependOnPatterns => AllBasePatterns;

        public override bool Resolve(List<GameTiles> groups, Hand hand, GameTile incoming, Scorings scorings) {
            int count = groups.SelectMany(tile => tile.ToTiles()).Count(tile => tile.Akadora);
            if (count > 0) {
                scorings.Add(new Scoring {
                    Type = ScoringType.Han,
                    Val = count,
                    Source = this
                });
                return true;
            }
            return false;
        }
    }
}
