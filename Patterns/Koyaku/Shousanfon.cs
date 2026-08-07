using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary> 小三風：两组风牌刻子加一组风牌雀头。1番。小四喜时由<see cref="Shousuushii"/>移除 </summary>
  public class Shousanfon : StdPattern {
    public Shousanfon(Base33332 base33332) {
      BaseOn(base33332);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      var windGroups = groups.Where(gr => gr.First.tile.IsWind).ToList();
      bool flag = windGroups.Any(gr => gr is Jantou)
          && windGroups.Count(gr => gr is Kou or Kan) >= 2;
      if (flag) {
        scores.Add(new Scoring(ScoringType.Han, 1, this));
        return true;
      }
      return false;
    }
  }
}
