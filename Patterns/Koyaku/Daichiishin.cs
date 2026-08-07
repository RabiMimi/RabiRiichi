using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  /// <summary> 大七星：七种字牌各一对的七对子。两倍役满，门前清限定 </summary>
  public class Daichiishin : StdPattern {
    public Daichiishin(Base72 base72, Chiitoitsu chiitoitsu, Tsuuiisou tsuuiisou) {
      BaseOn(base72);
      DependOn(chiitoitsu, tsuuiisou);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (!hand.menzen) {
        return false;
      }
      scores.Remove(afterPatterns);
      scores.Add(new Scoring(ScoringType.Yakuman, 2, this));
      return true;
    }
  }
}
