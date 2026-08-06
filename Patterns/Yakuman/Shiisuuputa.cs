using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  public class Shiisuuputa : StdPattern {
    public Shiisuuputa(BaseShiisuuputa baseShiisuuputa) {
      BaseOn(baseShiisuuputa);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
      return true;
    }
  }
}
