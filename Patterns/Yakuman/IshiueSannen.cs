using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
    public class IshiueSannen : StdPattern {
        public IshiueSannen(AllBasePatterns allBasePatterns, DoubleRiichi doubleRiichi, HaiteiRaoyue haiteiRaoyue, HouteiRaoyui houteiRaoyui) {
            BaseOn(allBasePatterns);
            DependOn(doubleRiichi);
            /// Cannot depend on these patterns because RinshanKaihou can be
            /// combined with IshiueSannen but not HaiteiRaoyue.
            After(haiteiRaoyue, houteiRaoyui);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (hand.game.wall.IsHaitei) {
                scores.Remove(afterPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
                return true;
            }
            return false;
        }
    }
}
