using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class Dora : StdPattern {
        public override PatternMask type => PatternMask.Bonus;

        public Dora(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            var tiles = groups.SelectMany(tile => tile.ToTiles());
            int han = 0;
            foreach (var tile in tiles) {
                han += hand.game.wall.CountDora(tile);
            }
            if (han > 0) {
                scores.Add(new Scoring(ScoringType.BonusHan, han, this));
                return true;
            }
            return false;
        }
    }
}
