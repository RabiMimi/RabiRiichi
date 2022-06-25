using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class Ryuuiisou : StdPattern {
        private static readonly List<Tile> MATCH_TILE = new Tiles("23468s6z");

        public Ryuuiisou(Base33332 base33332, Honitsu honitsu) {
            BaseOn(base33332);
            DependOn(honitsu);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            var tiles = groups.SelectMany(gr => gr);
            bool flag = tiles.All(tile => MATCH_TILE.Contains(tile.tile));
            if (flag) {
                scores.Remove(afterPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            }
            return flag;
        }
    }
}
