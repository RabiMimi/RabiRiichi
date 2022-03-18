using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 地和 : StdPattern {
        public 地和(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool flag = hand.game.IsFirstJun && !hand.player.IsBanker;
            if (flag) {
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            }
            return flag;
        }
    }
}
