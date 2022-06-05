using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class IshiueSannen : StdPattern {
        private readonly StdPattern[] haiteiYakus;

        public IshiueSannen(AllBasePatterns allBasePatterns, DoubleRiichi doubleRiichi, HaiteiRaoyue haiteiRaoyue, HouteiRaoyui houteiRaoyui) {
            BaseOn(allBasePatterns);
            DependOn(doubleRiichi);
            haiteiYakus = new StdPattern[] { haiteiRaoyue, houteiRaoyui };
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (hand.game.wall.IsHaitei) {
                scores.Remove(afterPatterns);
                scores.Remove(haiteiYakus);
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
                return true;
            }
            return false;
        }
    }
}
