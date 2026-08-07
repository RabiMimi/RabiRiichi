using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary> 黒一色：只由2/4/8筒与风牌构成。役满 </summary>
  public class Heiiisou : StdPattern {
    private static readonly Tiles MATCH_TILE = new("248p1234z");

    public Heiiisou(AllBasePatterns allBasePatterns, Honitsu honitsu) {
      BaseOn(allBasePatterns);
      // 全为风牌时不成立混一色，故只能After
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
