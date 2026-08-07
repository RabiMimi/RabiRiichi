using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary> 五門斉：牌型同时包含万子、筒子、索子、风牌、三元牌。2番 </summary>
  public class Uumensai : StdPattern {
    public Uumensai(AllBasePatterns allBasePatterns) {
      BaseOn(allBasePatterns);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      var tiles = groups.Select(gr => gr.First.tile).ToList();
      bool flag = tiles.Any(tile => tile.Suit == TileSuit.M)
          && tiles.Any(tile => tile.Suit == TileSuit.P)
          && tiles.Any(tile => tile.Suit == TileSuit.S)
          && tiles.Any(tile => tile.IsWind)
          && tiles.Any(tile => tile.IsSangen);
      if (flag) {
        scores.Add(new Scoring(ScoringType.Han, 2, this));
        return true;
      }
      return false;
    }
  }
}
