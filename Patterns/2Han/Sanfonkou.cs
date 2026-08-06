using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Sanfonkou : StdPattern {
    public Sanfonkou(Base33332 base33332) {
      BaseOn(base33332);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      int windKouCount = groups.Count(g => (g is Kou or Kan) && g.First.tile.IsWind);
      if (windKouCount == 3) {
        scores.Add(new Scoring(ScoringType.Han, 2, this));
        return true;
      }
      return false;
    }
  }
}
