using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Sanrenkou : StdPattern {
    public Sanrenkou(Base33332 base33332) {
      BaseOn(base33332);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      var kous = groups.Where(g => g is Kou or Kan).ToList();
      var bySuit = kous.GroupBy(g => g.Suit);
      foreach (var g in bySuit) {
        if (g.Key == TileSuit.Z) continue;
        var nums = g.Select(k => (int)k.First.tile.Num).OrderBy(n => n).ToList();
        for (int i = 0; i <= nums.Count - 3; i++) {
          if (nums[i + 1] == nums[i] + 1 && nums[i + 2] == nums[i] + 2) {
            scores.Add(new Scoring(ScoringType.Han, 2, this));
            return true;
          }
        }
      }
      return false;
    }
  }
}
