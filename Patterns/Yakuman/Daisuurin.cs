using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Daisuurin : StdPattern {
    private readonly Chiitoitsu _chiitoitsu;
    public Daisuurin(Base72 base72, Chiitoitsu chiitoitsu) {
      BaseOn(base72);
      DependOn(chiitoitsu);
      _chiitoitsu = chiitoitsu;
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      var allTiles = groups.SelectMany(g => g).Select(gt => gt.tile).ToList();
      bool match = allTiles.All(t => t.Suit == TileSuit.M && t.Num >= 2 && t.Num <= 8);
      if (match) {
        scores.Remove(_chiitoitsu);
        scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
        return true;
      }
      return false;
    }
  }
}
