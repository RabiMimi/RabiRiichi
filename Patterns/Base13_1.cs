using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {

  public class Base13_1 : BasePattern {
    /// <inheritdoc/>
    public override bool Resolve(Hand hand, GameTile incoming, out List<List<MenLike>> output) {
      output = null;
      // Check tile count
      if (hand.Count != (incoming == null ? Game.HAND_SIZE + 1 : Game.HAND_SIZE)) {
        return false;
      }
      // Check hand & groups valid
      var buckets = GetTileGroups(hand, incoming, true).GetAllBuckets();
      List<MenLike> ret = [];
      bool has2 = false;
      foreach (var gr in buckets) {
        if (gr.Count > 2 || !gr[0].tile.Is19Z) {
          return false;
        }
        if (gr.Count == 2) {
          if (has2) {
            return false;
          }
          has2 = true;
        }
        ret.Add(MenLike.From(gr));
      }
      output = [ret];
      return true;
    }

    /// <inheritdoc/>
    public override int Shanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten = 13) =>
        HandShapeAnalyzer.ThirteenOrphansShanten(hand, incoming, out output, maxShanten);
  }
}
