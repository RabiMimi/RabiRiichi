using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Shousanfon : StdPattern {
    public Shousanfon(Base33332 base33332) {
      BaseOn(base33332);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      int kouCount = groups.Count(g => (g is Kou or Kan) && g.First.tile.IsWind);
      int pairCount = groups.Count(g => g is Jantou && g.First.tile.IsWind);
      if (kouCount == 2 && pairCount == 1) {
        scores.Add(new Scoring(ScoringType.Han, 2, this));
        return true;
      }
      return false;
    }
  }
}
