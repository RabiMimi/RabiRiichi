using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class 役牌自风 : 役牌 {
        protected override Tile YakuTile => yakuTile;
        private Tile yakuTile;
        public 役牌自风(Base33332 base33332) : base(base33332) { }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            yakuTile = Tile.From(hand.player.Wind);
            return base.Resolve(groups, hand, incoming, scores);
        }
    }
}
