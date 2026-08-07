using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary> 三風刻：三组风牌刻子。2番。小四喜/大四喜时由上位役移除 </summary>
  public class Sanfonkou : StdPattern {
    public Sanfonkou(Base33332 base33332) {
      BaseOn(base33332);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (groups.Count(gr => gr is Kou or Kan && gr.First.tile.IsWind) < 3) {
        return false;
      }
      scores.Add(new Scoring(ScoringType.Han, 2, this));
      return true;
    }
  }
}
