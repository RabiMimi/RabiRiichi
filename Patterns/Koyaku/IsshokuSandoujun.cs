using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary> 一色三同順：三组完全相同的顺子。3番，副露减一番，与一盃口不复合 </summary>
  public class IsshokuSandoujun : StdPattern {
    public IsshokuSandoujun(Base33332 base33332, Iipeikou iipeikou) {
      BaseOn(base33332);
      After(iipeikou);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      bool isValid = groups
          .OfType<Shun>()
          .GroupBy(gr => gr.Value)
          .Any(gr => gr.Count() >= 3);
      if (isValid) {
        scores.Remove(afterPatterns);
        scores.Add(new Scoring(ScoringType.Han, hand.menzen ? 3 : 2, this));
        return true;
      }
      return false;
    }
  }
}
