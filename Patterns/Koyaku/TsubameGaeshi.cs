using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  /// <summary> 燕返し：荣和他家的立直宣言牌。1番 </summary>
  public class TsubameGaeshi : StdPattern {
    public override PatternMask type => PatternMask.Luck;

    public TsubameGaeshi(AllBasePatterns allBasePatterns) {
      BaseOn(allBasePatterns);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (incoming.discardInfo?.isRiichiDeclaration == true) {
        scores.Add(new Scoring(ScoringType.Han, 1, this));
        return true;
      }
      return false;
    }
  }
}
