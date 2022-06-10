using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
    public class YakuhaiJikaze : Yakuhai {
        protected override Tile YakuTile => yakuTile;
        private Tile yakuTile;
        public YakuhaiJikaze(Base33332 base33332) : base(base33332) { }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            yakuTile = Tile.From(hand.player.Wind);
            return base.Resolve(groups, hand, incoming, scores);
        }
    }
}
