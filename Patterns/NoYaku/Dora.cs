using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Dora : StdPattern {
    public override PatternMask type => PatternMask.Bonus;

    public Dora(AllBasePatterns allBasePatterns) {
      BaseOn(allBasePatterns);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      // Pulled-aside nukidora tiles are set apart from the winning hand groups
      // but are still owned by the player, so they earn regular dora too (e.g.
      // when the indicator is West and North becomes a dora).
      var tiles = groups.SelectMany(tile => tile.ToTiles())
          .Concat(hand.nukiDora.Select(t => t.tile));
      int han = 0;
      foreach (var tile in tiles) {
        han += hand.game.wall.CountDora(tile);
      }
      if (han > 0) {
        scores.Add(new Scoring(ScoringType.BonusHan, han, this));
        return true;
      }
      return false;
    }
  }
}