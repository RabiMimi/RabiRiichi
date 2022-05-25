using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class HaiteiRaoyue : StdPattern {
        public override PatternMask type => PatternMask.Luck;

        public HaiteiRaoyue(AllBasePatterns allBasePatterns) {
            BaseOn(allBasePatterns);
        }
        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (incoming.IsTsumo && hand.game.wall.IsHaitei && incoming.source == TileSource.Wall) {
                scores.Add(new Scoring(ScoringType.Han, 1, this));
                return true;
            }
            return false;
        }
    }
}