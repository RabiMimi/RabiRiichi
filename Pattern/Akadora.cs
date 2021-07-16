using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Akadora : BonusPattern {
        private Type[] mBasePatterns = new Type[] { typeof(Base33332) };
        public override Type[] basePatterns => mBasePatterns;

        private Type[] mDependOnPatterns = new Type[0];
        public override Type[] dependOnPatterns => mDependOnPatterns;

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
