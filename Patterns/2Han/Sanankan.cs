using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Sanankan : StdPattern {
    private readonly Ryanankan _ryanankan;
    public Sanankan(Base33332 base33332, Ryanankan ryanankan) {
      BaseOn(base33332);
      After(ryanankan);
      _ryanankan = ryanankan;
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      int ankanCount = hand.called.Count(gr => gr is Kan kan && kan.KanSource == TileSource.Ankan);
      if (ankanCount == 3) {
        scores.Remove(_ryanankan);
        scores.Add(new Scoring(ScoringType.Han, 2, this));
        return true;
      }
      return false;
    }
  }
}
