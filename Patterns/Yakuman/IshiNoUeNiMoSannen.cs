using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  public class IshiNoUeNiMoSannen : StdPattern {
    public override PatternMask type => PatternMask.Luck;

    private readonly DoubleRiichi _doubleRiichi;
    private readonly HaiteiRaoyue _haitei;
    private readonly HouteiRaoyui _houtei;

    public IshiNoUeNiMoSannen(AllBasePatterns allBasePatterns, DoubleRiichi doubleRiichi, HaiteiRaoyue haitei, HouteiRaoyui houtei) {
      BaseOn(allBasePatterns);
      After(doubleRiichi, haitei, houtei);
      _doubleRiichi = doubleRiichi;
      _haitei = haitei;
      _houtei = houtei;
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (hand.wRiichi && hand.game.wall.IsHaitei) {
        scores.Remove(_doubleRiichi);
        scores.Remove(_haitei);
        scores.Remove(_houtei);
        scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
        return true;
      }
      return false;
    }
  }
}
