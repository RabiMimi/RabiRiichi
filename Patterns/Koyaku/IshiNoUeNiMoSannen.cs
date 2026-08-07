using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  /// <summary> 石の上にも三年：双立直后以海底/河底和了。役满 </summary>
  public class IshiNoUeNiMoSannen : StdPattern {
    public override PatternMask type => PatternMask.Luck;

    public IshiNoUeNiMoSannen(AllBasePatterns allBasePatterns, DoubleRiichi doubleRiichi, HaiteiRaoyue haiteiRaoyue, HouteiRaoyui houteiRaoyui) {
      BaseOn(allBasePatterns);
      DependOn(doubleRiichi);
      /// Cannot depend on these patterns because RinshanKaihou can be
      /// combined with IshiNoUeNiMoSannen but not HaiteiRaoyue.
      After(haiteiRaoyue, houteiRaoyui);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (hand.game.wall.IsHaitei) {
        scores.Remove(afterPatterns);
        scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
        return true;
      }
      return false;
    }
  }
}