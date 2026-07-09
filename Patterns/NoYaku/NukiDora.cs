using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  /// <summary>
  /// 拔北宝牌：每拔出一张北记1番宝牌。留在和牌手牌里作为面子/雀头的北不算拔北宝牌，
  /// 它只作为客风处理——若指示牌为西，这样的北会计入普通宝牌（由<see cref="Dora"/>
  /// 结算），但不计拔北。
  /// </summary>
  public class NukiDora : StdPattern {
    public override PatternMask type => PatternMask.Bonus;

    public NukiDora(AllBasePatterns allBasePatterns) {
      BaseOn(allBasePatterns);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      int han = hand.nukiDora.Count;
      if (han > 0) {
        scores.Add(new Scoring(ScoringType.BonusHan, han, this));
        return true;
      }
      return false;
    }
  }
}
