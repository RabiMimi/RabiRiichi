using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class Uradora : StdPattern {
        public override PatternMask type => PatternMask.Bonus;

        public Uradora(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (!hand.riichi)
                return false;

            var tiles = groups.SelectMany(tile => tile.ToTiles());
            int han = 0;
            foreach (var tile in tiles) {
                han += hand.game.wall.CountUradora(tile);
            }
            scores.Add(new Scoring(ScoringType.BonusHan, han, this));
            return true;
        }
    }
}
