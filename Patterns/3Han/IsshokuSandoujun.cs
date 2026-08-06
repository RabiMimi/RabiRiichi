using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class IsshokuSandoujun : StdPattern {
    public IsshokuSandoujun(Base33332 base33332) {
      BaseOn(base33332);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      var shuns = groups.OfType<Shun>().ToList();
      var bySuitAndVal = shuns.GroupBy(s => (s.Suit, s.First.tile.Num));
      foreach (var g in bySuitAndVal) {
        if (g.Count() >= 3) {
          scores.Add(new Scoring(ScoringType.Han, hand.menzen ? 3 : 2, this));
          return true;
        }
      }
      return false;
    }
  }
}
