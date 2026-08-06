using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Chinpaikou : StdPattern {
    public Chinpaikou(Base33332 base33332) {
      BaseOn(base33332);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      var kous = groups.Where(g => g is Kou or Kan).ToList();
      var bySuit = kous.GroupBy(g => g.Suit);
      foreach (var g in bySuit) {
        if (g.Key == TileSuit.Z) continue;
        var nums = g.Select(k => (int)k.First.tile.Num).ToList();
        for (int i = 0; i < nums.Count; i++) {
          for (int j = i + 1; j < nums.Count; j++) {
            if (Math.Abs(nums[i] - nums[j]) == 3) {
              scores.Add(new Scoring(ScoringType.Han, 1, this));
              return true;
            }
          }
        }
      }
      return false;
    }
  }
}
