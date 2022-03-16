using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class 河底捞鱼 : StdPattern {
        public 河底捞鱼(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }
        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            // 应该不会错，不用排除花天月底
            if (!incoming.IsTsumo && hand.game.wall.IsHaitei) {
                scores.Add(new Scoring(ScoringType.Han, 1, this));
                return true;
            }
            return false;
        }
    }
}