using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RabiRiichi.Riichi;

namespace RabiRiichi.Pattern {
    public abstract class 役牌 : StdPattern {
        public override sealed Type[] dependOnPatterns => Only33332;

        protected abstract Tile YakuTile { get; }

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            var gr = groups.Where(tiles => (tiles is Kou || tiles is Kan) && tiles.HasTile(YakuTile));
            if (gr.Count() > 0) {
                scorings.Add(new Scoring(ScoringType.Han, 1, this));
                return true;
            }
            return false;
        }
    }
}
