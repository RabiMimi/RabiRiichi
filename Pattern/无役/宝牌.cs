using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 宝牌 : StdPattern {
        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            var tiles = groups.SelectMany(tile => tile.ToTiles());
            int han = 0;
            foreach (var tile in tiles) {
                han += hand.game.wall.CountDora(tile);
            }
            if (han > 0) {
                scores.Add(new Scoring(ScoringType.Han, han, this));
                return true;
            }
            return false;
        }
    }
}
