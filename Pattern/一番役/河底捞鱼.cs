using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class 河底捞鱼 : StdPattern {
        public 河底捞鱼(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }
        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (!incoming.IsTsumo && hand.game.wall.IsHaitei) {
                scores.Add(new Scoring(ScoringType.Han, 1, this));
                return true;
            }
            return false;
        }
    }
}