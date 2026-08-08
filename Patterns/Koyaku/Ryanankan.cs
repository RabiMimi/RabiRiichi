using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary> 二暗槓：开出两个暗杠。2番，手牌其余部分可副露 </summary>
  public class Ryanankan : StdPattern {
    public Ryanankan(Base33332 base33332) {
      BaseOn(base33332);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (groups.OfType<Kan>().Count(kan => kan.IsClose) >= 2) {
        scores.Add(new Scoring(ScoringType.Han, 2, this));
        return true;
      }
      return false;
    }
  }
}
