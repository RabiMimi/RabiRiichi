using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Ryanankan : StdPattern {
    public Ryanankan(Base33332 base33332) {
      BaseOn(base33332);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      int ankanCount = hand.called.Count(gr => gr is Kan kan && kan.KanSource == TileSource.Ankan);
      if (ankanCount == 2) {
        scores.Add(new Scoring(ScoringType.Han, 1, this));
        return true;
      }
      return false;
    }
  }
}
