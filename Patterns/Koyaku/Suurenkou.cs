using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  /// <summary> 四連刻：四组同花色且序数连续的刻子。役满 </summary>
  public class Suurenkou : StdPattern {
    public Suurenkou(Base33332 base33332, Sanrenkou sanrenkou, Toitoi toitoi) {
      BaseOn(base33332);
      DependOn(sanrenkou, toitoi);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (KouSeries.Exists(groups, 4, 1)) {
        scores.Remove(afterPatterns);
        scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
        return true;
      }
      return false;
    }
  }
}
