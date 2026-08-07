using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  /// <summary> 花天月地：牌山剩最后一张时开杠，以岭上牌自摸和了。役满 </summary>
  public class Katengecchi : StdPattern {
    public override PatternMask type => PatternMask.Luck;

    public Katengecchi(AllBasePatterns allBasePatterns, RinshanKaihou rinshanKaihou) {
      BaseOn(allBasePatterns);
      DependOn(rinshanKaihou);
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
