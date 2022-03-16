using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 地和 : StdPattern {
        public 地和(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool flag = hand.jun == 1
                && hand.game.players.All(player => player.hand.jun <= 1 && player.hand.menzen)
                && hand.player.wind != hand.game.info.wind;
            if (flag) {
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            }
            return flag;
        }
    }
}
