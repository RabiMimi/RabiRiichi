using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  /// <summary> 八連荘：庄家连庄和了八次，第八次和了算役满 </summary>
  public class Paarenchan : StdPattern {
    /// <summary> 成立所需的连庄和了次数 </summary>
    public const int REQUIRED_STREAK = 8;

    public override PatternMask type => PatternMask.Bonus;

    public Paarenchan(AllBasePatterns allBasePatterns) {
      BaseOn(allBasePatterns);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (hand.player.IsDealer && hand.player.agariStreak + 1 >= REQUIRED_STREAK) {
        scores.Add(new Scoring(ScoringType.BonusYakuman, 1, this));
        return true;
      }
      return false;
    }
  }
}
