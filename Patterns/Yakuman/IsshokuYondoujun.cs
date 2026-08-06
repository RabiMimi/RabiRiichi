using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class IsshokuYondoujun : StdPattern {
    private readonly IsshokuSandoujun _sandoujun;
    private readonly Iipeikou _iipeikou;
    public IsshokuYondoujun(Base33332 base33332, IsshokuSandoujun sandoujun, Iipeikou iipeikou) {
      BaseOn(base33332);
      After(sandoujun, iipeikou);
      _sandoujun = sandoujun;
      _iipeikou = iipeikou;
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      var shuns = groups.OfType<Shun>().ToList();
      var bySuitAndVal = shuns.GroupBy(s => (s.Suit, s.First.tile.Num));
      foreach (var g in bySuitAndVal) {
        if (g.Count() >= 4) {
          scores.Remove(_sandoujun);
          scores.Remove(_iipeikou);
          scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
          return true;
        }
      }
      return false;
    }
  }
}
