using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 断幺九 : StdPattern {
        public 断幺九(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }
        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (groups.SelectMany(gr => gr).Any(tile => tile.tile.Is19Z))
                return false;
            if (!hand.game.config.allowKuitan && !hand.menzen)
                return false;
            scores.Add(new Scoring(ScoringType.Han, 1, this));
            return true;
        }
    }
}
