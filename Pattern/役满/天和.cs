using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 天和 : StdPattern {
        public 天和(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool flag = hand.game.players.All(player => player.hand.jun == 0);
            if (flag) {
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            }
            return flag;
        }
    }
}
