using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary> 一色四同順：四组完全相同的顺子。役满 </summary>
  public class IsshokuYondoujun : StdPattern {
    public IsshokuYondoujun(Base33332 base33332, IsshokuSandoujun isshokuSandoujun, Ryanpeikou ryanpeikou) {
      BaseOn(base33332);
      DependOn(isshokuSandoujun);
      After(ryanpeikou);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      bool isValid = groups
          .OfType<Shun>()
          .GroupBy(gr => gr.Value)
          .Any(gr => gr.Count() >= 4);
      if (isValid) {
        scores.Remove(afterPatterns);
        scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
        return true;
      }
      return false;
    }
  }
}
