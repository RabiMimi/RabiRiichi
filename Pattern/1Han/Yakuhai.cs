using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public abstract class Yakuhai : StdPattern {
        public Yakuhai(Base33332 base33332) {
            BaseOn(base33332);
        }

        protected abstract Tile YakuTile { get; }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool hasYaku = groups.Any(tiles => (tiles is Kou || tiles is Kan) && tiles.HasTile(YakuTile));
            if (hasYaku) {
                scores.Add(new Scoring(ScoringType.Han, 1, this));
                return true;
            }
            return false;
        }
    }
}
