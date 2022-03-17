using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 绿一色 : StdPattern {
        private static readonly List<Tile> MATCH_TILE = new Tiles("23468s6z");

        public 绿一色(Base33332 base33332, 混一色 混一色) {
            BaseOn(base33332);
            DependOn(混一色);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            var tiles = groups.SelectMany(gr => gr);
            bool flag = tiles.All(tile => MATCH_TILE.Contains(tile.tile));
            if (flag) {
                scores.Remove(dependOnPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            }
            return flag;
        }
    }
}
