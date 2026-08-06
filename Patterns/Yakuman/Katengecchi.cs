using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  public class Katengecchi : StdPattern {
    public override PatternMask type => PatternMask.Luck;

    private readonly RinshanKaihou _rinshan;

    public Katengecchi(AllBasePatterns allBasePatterns, RinshanKaihou rinshan) {
      BaseOn(allBasePatterns);
      After(rinshan);
      _rinshan = rinshan;
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      if (incoming.source == TileSource.Wanpai && hand.game.wall.IsHaitei) {
        scores.Remove(_rinshan);
        scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
        return true;
      }
      return false;
    }
  }
}
