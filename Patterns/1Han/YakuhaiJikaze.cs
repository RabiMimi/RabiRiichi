using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  public class YakuhaiJikaze(Base33332 base33332) : Yakuhai(base33332) {
    protected override Tile YakuTile => yakuTile;
    private Tile yakuTile;

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      yakuTile = Tile.From(hand.player.Wind);
      return base.Resolve(groups, hand, incoming, scores);
    }
  }
}