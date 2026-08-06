using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Daichiishin : StdPattern {
    private readonly Chiitoitsu _chiitoitsu;
    private readonly Tsuuiisou _tsuuiisou;
    public Daichiishin(Base72 base72, Chiitoitsu chiitoitsu, Tsuuiisou tsuuiisou) {
      BaseOn(base72);
      DependOn(chiitoitsu);
      After(tsuuiisou);
      _chiitoitsu = chiitoitsu;
      _tsuuiisou = tsuuiisou;
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      var allTiles = groups.SelectMany(g => g).Select(gt => gt.tile).ToList();
      bool match = allTiles.All(t => t.Suit == TileSuit.Z);
      if (match) {
        scores.Remove(_chiitoitsu);
        scores.Remove(_tsuuiisou);
        scores.Add(new Scoring(ScoringType.Yakuman, 2, this));
        return true;
      }
      return false;
    }
  }
}
