using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary> 清盃口：两种花色、序数相同的顺子构成的二盃口。门前清限定，与二盃口复合加计3番 </summary>
  public class Chinpeikou : StdPattern {
    public Chinpeikou(Base33332 base33332, Ryanpeikou ryanpeikou) {
      BaseOn(base33332);
      DependOn(ryanpeikou);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (!hand.menzen) {
        return false;
      }

      var shunTypes = groups
          .OfType<Shun>()
          .GroupBy(gr => gr.Value)
          .ToList();
      if (shunTypes.Count != 2) {
        return false;
      }
      var first = shunTypes[0].First();
      var second = shunTypes[1].First();
      if (first.Suit == second.Suit || first.First.tile.Num != second.First.tile.Num) {
        return false;
      }
      scores.Add(new Scoring(ScoringType.Han, 3, this));
      return true;
    }
  }
}
