using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  /// <summary> 筋牌刻：三组同花色、序数成筋（147/258/369）的刻子。2番 </summary>
  public class Chinpaikou : StdPattern {
    public Chinpaikou(Base33332 base33332) {
      BaseOn(base33332);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (KouSeries.Exists(groups, 3, 3)) {
        scores.Add(new Scoring(ScoringType.Han, 2, this));
        return true;
      }
      return false;
    }
  }
}
