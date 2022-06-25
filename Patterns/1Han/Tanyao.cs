using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Util;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class Tanyao : StdPattern {
        public Tanyao(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }
        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (groups.SelectMany(gr => gr).Any(tile => tile.tile.Is19Z))
                return false;
            if (!hand.game.config.agariOption.HasAnyFlag(AgariOption.Kuitan) && !hand.menzen)
                return false;
            scores.Add(new Scoring(ScoringType.Han, 1, this));
            return true;
        }
    }
}
