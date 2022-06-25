using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class Akadora : StdPattern {
        public override PatternMask type => PatternMask.Bonus;

        public Akadora(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            int count = groups.SelectMany(tile => tile.ToTiles()).Count(tile => tile.Akadora);
            if (count > 0) {
                scores.Add(new Scoring(ScoringType.BonusHan, count, this));
                return true;
            }
            return false;
        }
    }
}
