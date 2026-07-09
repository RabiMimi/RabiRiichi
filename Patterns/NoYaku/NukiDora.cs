using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary>
  /// 拔北宝牌：手里每有一个北记1番宝牌（类似赤宝牌）。北无论被拔出、还是作为面子/雀头
  /// 留在和牌里都计入。北本身作为客风处理，若指示牌为西，北还会额外计入普通宝牌（由
  /// <see cref="Dora"/>另行结算，两者叠加）。
  /// </summary>
  public class NukiDora : StdPattern {
    public override PatternMask type => PatternMask.Bonus;

    public NukiDora(AllBasePatterns allBasePatterns) {
      BaseOn(allBasePatterns);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      int inHand = groups
          .SelectMany(group => group.ToTiles())
          .Count(tile => tile.IsSame(Tile.North));
      int han = hand.nukiDora.Count + inHand;
      if (han > 0) {
        scores.Add(new Scoring(ScoringType.BonusHan, han, this));
        return true;
      }
      return false;
    }
  }
}
