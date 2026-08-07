using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary> 三暗槓：开出三个暗杠。役满，手牌其余部分可副露 </summary>
  public class Sanankan : StdPattern {
    public Sanankan(Base33332 base33332, Ryanankan ryanankan, Sankantsu sankantsu) {
      BaseOn(base33332);
      DependOn(ryanankan);
      After(sankantsu);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (groups.OfType<Kan>().Count(kan => kan.IsClose) >= 3) {
        scores.Remove(afterPatterns);
        scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
        return true;
      }
      return false;
    }
  }
}
