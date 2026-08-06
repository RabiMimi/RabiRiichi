using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  public class TsubameGaeshi : StdPattern {
    public override PatternMask type => PatternMask.Luck;

    public TsubameGaeshi(AllBasePatterns allBasePatterns) {
      BaseOn(allBasePatterns);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (incoming != null && !incoming.IsTsumo && incoming.discardInfo != null &&
          incoming.discardInfo.fromPlayer.hand.riichiTile == incoming) {
        scores.Add(new Scoring(ScoringType.Han, 1, this));
        return true;
      }
      return false;
    }
  }
}
