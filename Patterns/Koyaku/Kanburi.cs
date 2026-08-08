using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  /// <summary> 槓振り：荣和他家开杠补摸岭上牌后打出的牌，不限于摸切的岭上牌。1番 </summary>
  public class Kanburi : StdPattern {
    public override PatternMask type => PatternMask.Luck;

    public Kanburi(AllBasePatterns allBasePatterns) {
      BaseOn(allBasePatterns);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (incoming.discardInfo?.reason == DiscardReason.DrawRinshan) {
        scores.Add(new Scoring(ScoringType.Han, 1, this));
        return true;
      }
      return false;
    }
  }
}
