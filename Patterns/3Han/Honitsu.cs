using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class Honitsu : StdPattern {
        public Honitsu(AllExcept13_1 allExcept13_1) {
            BaseOn(allExcept13_1);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool Honitsu = groups
                .SelectMany(gr => gr)
                .Where(tile => tile.tile.IsMPS)
                .GroupBy(tile => tile.tile.Suit)
                .Count() == 1;
            if (Honitsu) {
                scores.Add(new Scoring(ScoringType.Han, hand.menzen ? 3 : 2, this));
                return true;
            }
            return false;
        }
    }
}
