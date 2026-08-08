using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  /// <summary> 人和：闲家在自己第一次摸牌前荣和。役满，门前清限定 </summary>
  public class Renhou : StdPattern {
    public override PatternMask type => PatternMask.Luck;

    public Renhou(AllBasePatterns allBasePatterns) {
      BaseOn(allBasePatterns);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      bool flag = !incoming.IsTsumo
          && !hand.player.IsDealer
          && hand.jun == 0
          && hand.game.IsFirstJun;
      if (flag) {
        scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
      }
      return flag;
    }
  }
}
