using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Uradora : StdPattern {
        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (!hand.riichi)
                return false;

            var tiles = groups.SelectMany(tile => tile.ToTiles());
            int han = 0;
            foreach (var tile in tiles) {
                han += hand.game.wall.CountUradora(tile);
            }
            scores.Add(new Scoring(ScoringType.Han, han, this));
            return true;
        }
    }
}
