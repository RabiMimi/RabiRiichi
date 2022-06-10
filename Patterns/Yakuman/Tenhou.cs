using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
    public class Tenhou : StdPattern {
        public Tenhou(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool flag = hand.jun == 1 && hand.player.IsDealer;
            if (flag) {
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            }
            return flag;
        }
    }
}
