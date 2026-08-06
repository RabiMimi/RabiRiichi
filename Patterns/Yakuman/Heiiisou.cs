using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Heiiisou : StdPattern {
    public Heiiisou(AllBasePatterns allBasePatterns) {
      BaseOn(allBasePatterns);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      var allTiles = groups.SelectMany(g => g).Select(gt => gt.tile).ToList();
      bool match = allTiles.All(t => 
        (t.Suit == TileSuit.S && (t.Num == 2 || t.Num == 4 || t.Num == 8)) ||
        (t.Suit == TileSuit.Z && t.Num >= 1 && t.Num <= 4)
      );
      if (match) {
        scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
        return true;
      }
      return false;
    }
  }
}
