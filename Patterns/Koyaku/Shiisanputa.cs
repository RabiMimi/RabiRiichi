using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary> 十三不塔：起手除一组对子外，任意两张牌都不成对子/搭子/面子。役满 </summary>
  public class Shiisanputa : StdPattern {
    public override PatternMask type => PatternMask.Luck;

    public Shiisanputa(BaseButa baseButa) {
      BaseOn(baseButa);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (!groups.Any(gr => gr is Jantou)) {
        return false;
      }
      scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
      return true;
    }
  }
}
