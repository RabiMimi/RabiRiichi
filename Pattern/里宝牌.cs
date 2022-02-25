using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 里宝牌 : StdPattern {
        public override Type[] basePatterns => AllBasePatterns;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (!hand.riichi)
                return false;

            var tiles = groups.SelectMany(tile => tile.ToTiles());
            int han = 0;
            foreach (var tile in tiles) {
                han += hand.game.wall.CountUradora(tile);
            }
            scorings.Add(new Scoring(ScoringType.Han, han, this));
            return true;
        }
    }
}
