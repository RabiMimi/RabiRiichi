using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Suurenkou : StdPattern {
    private readonly Sanrenkou _sanrenkou;
    public Suurenkou(Base33332 base33332, Sanrenkou sanrenkou) {
      BaseOn(base33332);
      After(sanrenkou);
      _sanrenkou = sanrenkou;
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      var kous = groups.Where(g => g is Kou or Kan).ToList();
      var bySuit = kous.GroupBy(g => g.Suit);
      foreach (var g in bySuit) {
        if (g.Key == TileSuit.Z) continue;
        var nums = g.Select(k => (int)k.First.tile.Num).OrderBy(n => n).ToList();
        for (int i = 0; i <= nums.Count - 4; i++) {
          if (nums[i + 1] == nums[i] + 1 && nums[i + 2] == nums[i] + 2 && nums[i + 3] == nums[i] + 3) {
            scores.Remove(_sanrenkou);
            scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
            return true;
          }
        }
      }
      return false;
    }
  }
}
