using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Uumensai : StdPattern {
    public Uumensai(Base33332 base33332) {
      BaseOn(base33332);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (groups.Count != 5) {
        return false;
      }
      var categories = new HashSet<int>();
      foreach (var g in groups) {
        int cat = -1;
        if (g.Suit == TileSuit.M) cat = 0;
        else if (g.Suit == TileSuit.P) cat = 1;
        else if (g.Suit == TileSuit.S) cat = 2;
        else if (g.Suit == TileSuit.Z) {
          if (g.First.tile.IsWind) cat = 3;
          else if (g.First.tile.IsSangen) cat = 4;
        }
        if (cat == -1 || !categories.Add(cat)) {
          return false;
        }
      }
      scores.Add(new Scoring(ScoringType.Han, 2, this));
      return true;
    }
  }
}
