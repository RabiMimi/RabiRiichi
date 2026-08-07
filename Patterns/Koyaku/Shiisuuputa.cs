using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary> 十四不塔：起手14张完全散乱，连对子都没有。役满 </summary>
  public class Shiisuuputa : StdPattern {
    public override PatternMask type => PatternMask.Luck;

    public Shiisuuputa(BaseButa baseButa) {
      BaseOn(baseButa);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (groups.Any(gr => gr is not Musou)) {
        return false;
      }
      scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
      return true;
    }
  }
}
