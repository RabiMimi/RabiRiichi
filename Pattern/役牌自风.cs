using System;
using System.Collections.Generic;
using System.Text;
using RabiRiichi.Riichi;

namespace RabiRiichi.Pattern {
    public class 役牌自风 : 役牌 {
        protected override Tile YakuTile => yakuTile;
        private Tile yakuTile;

        public override bool Resolve(List<GameTiles> groups, Hand hand, GameTile incoming, Scorings scorings) {
            yakuTile = Tile.From(hand.player.wind);
            return base.Resolve(groups, hand, incoming, scorings);
        }
    }
}
