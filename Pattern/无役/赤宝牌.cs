using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 赤宝牌 : StdPattern {
        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            int count = groups.SelectMany(tile => tile.ToTiles()).Count(tile => tile.Akadora);
            if (count > 0) {
                scorings.Add(new Scoring(ScoringType.Han, count, this));
                return true;
            }
            return false;
        }
    }
}
