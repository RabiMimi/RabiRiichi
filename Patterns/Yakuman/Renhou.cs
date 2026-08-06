using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  public class Renhou : StdPattern {
    public Renhou(AllBasePatterns allBasePatterns) {
      BaseOn(allBasePatterns);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      bool flag = incoming != null && !incoming.IsTsumo && hand.game.IsFirstJun && !hand.player.IsDealer;
      if (flag) {
        scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
      }
      return flag;
    }
  }
}
