using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  /// <summary> 三連刻：三组同花色且序数连续的刻子。2番 </summary>
  public class Sanrenkou : StdPattern {
    public Sanrenkou(Base33332 base33332) {
      BaseOn(base33332);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (KouSeries.Exists(groups, 3, 1)) {
        scores.Add(new Scoring(ScoringType.Han, 2, this));
        return true;
      }
      return false;
    }
  }
}
