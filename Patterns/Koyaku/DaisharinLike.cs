using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary> 車輪系（大車輪/大竹林/大数隣）：同一花色2~8的七对子。役满，门前清限定 </summary>
  public abstract class DaisharinLike : StdPattern {
    /// <summary> 构成本役的花色 </summary>
    protected abstract TileSuit MatchSuit { get; }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (!hand.menzen) {
        return false;
      }
      bool flag = groups
          .SelectMany(gr => gr)
          .All(gameTile => gameTile.tile.Suit == MatchSuit
              && gameTile.tile.Num >= 2
              && gameTile.tile.Num <= 8);
      if (flag) {
        scores.Remove(afterPatterns);
        scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
      }
      return flag;
    }
  }
}
