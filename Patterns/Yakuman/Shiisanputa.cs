using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  public class Shiisanputa : StdPattern {
    public Shiisanputa(BaseShiisanputa baseShiisanputa) {
      BaseOn(baseShiisanputa);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
      return true;
    }
  }
}
