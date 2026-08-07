using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Shousuushii : StdPattern {
    public Shousuushii(Base33332 base33332, Sanfonkou sanfonkou, Shousanfon shousanfon) {
      BaseOn(base33332);
      // 小四喜的牌型同时满足这两个古役
      After(sanfonkou, shousanfon);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      var windGroups = groups.Where(gr => gr.First.tile.IsWind);
      bool flag = windGroups.Any(gr => gr is Jantou)
          && windGroups.GroupBy(gr => gr.First.tile).Count() == 4;
      if (flag) {
        scores.Remove(afterPatterns);
        scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
        return true;
      }
      return false;
    }
  }
}