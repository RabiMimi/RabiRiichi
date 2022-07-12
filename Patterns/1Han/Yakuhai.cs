using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public abstract class Yakuhai : StdPattern {
        public Yakuhai(Base33332 base33332) {
            BaseOn(base33332);
        }

        protected abstract Tile YakuTile { get; }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            int yakuCount = groups.Count(tiles => (tiles is Kou || tiles is Kan) && tiles.First.tile.IsSame(YakuTile));
            if (yakuCount > 0) {
                scores.Add(new Scoring(ScoringType.Han, yakuCount, this));
                return true;
            }
            return false;
        }
    }
}
