using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 赤宝牌 : StdPattern {
        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            int count = groups.SelectMany(tile => tile.ToTiles()).Count(tile => tile.Akadora);
            if (count > 0) {
                scores.Add(new Scoring(ScoringType.Han, count, this));
                return true;
            }
            return false;
        }
    }
}
