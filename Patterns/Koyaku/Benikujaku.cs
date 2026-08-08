using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary> 紅孔雀：只由1/5/7/9索与中构成。役满 </summary>
  public class Benikujaku : StdPattern {
    private static readonly Tiles MATCH_TILE = new("1579s7z");

    public Benikujaku(AllBasePatterns allBasePatterns, Honitsu honitsu) {
      BaseOn(allBasePatterns);
      After(honitsu);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      bool flag = groups.SelectMany(gr => gr).All(gameTile => MATCH_TILE.ContainsIgnoreDora(gameTile.tile));
      if (flag) {
        scores.Remove(afterPatterns);
        scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
      }
      return flag;
    }
  }
}
