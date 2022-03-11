using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class 役牌场风 : 役牌 {
        protected override Tile YakuTile => yakuTile;
        private Tile yakuTile;
        public 役牌场风(Base33332 base33332) : base(base33332) { }

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            yakuTile = Tile.From(hand.game.info.wind);
            return base.Resolve(groups, hand, incoming, scorings);
        }
    }
}
