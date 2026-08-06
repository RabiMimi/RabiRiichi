using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Chinpeikou : StdPattern {
    private readonly Ryanpeikou _ryanpeikou;
    private readonly Iipeikou _iipeikou;
    public Chinpeikou(Base33332 base33332, Ryanpeikou ryanpeikou, Iipeikou iipeikou) {
      BaseOn(base33332);
      DependOn(ryanpeikou);
      After(iipeikou);
      _ryanpeikou = ryanpeikou;
      _iipeikou = iipeikou;
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (!hand.menzen) {
        return false;
      }

      var shuns = groups.OfType<Shun>().ToList();
      if (shuns.Count != 4) {
        return false;
      }

      bool isSameSuit = shuns.Select(s => s.Suit).Distinct().Count() == 1;
      if (isSameSuit) {
        scores.Remove(_ryanpeikou);
        scores.Remove(_iipeikou);
        scores.Add(new Scoring(ScoringType.Han, 3, this));
        return true;
      }
      return false;
    }
  }
}
