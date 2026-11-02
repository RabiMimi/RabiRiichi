using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {

  public class Base72 : BasePattern {
    /// <inheritdoc/>
    public override bool Resolve(Hand hand, GameTile incoming, out List<List<MenLike>> output) {
      output = null;
      // Check tile count
      if (hand.Count != (incoming == null ? Game.HAND_SIZE + 1 : Game.HAND_SIZE)) {
        return false;
      }
      // Check groups
      if (hand.called.Any(gr => gr is not Jantou)) {
        return false;
      }
      // Check hand & groups valid
      var tileGroups = GetTileGroups(hand, incoming, true);
      var ret = new List<MenLike>();
      foreach (var gr in tileGroups.GetAllBuckets()) {
        if (gr.Count != 2) {
          return false;
        }
        ret.Add(MenLike.From(gr));
      }
      output = [ret];
      return true;
    }

    /// <inheritdoc/>
    public override int Shanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten = 13) =>
        HandShapeAnalyzer.SevenPairsShanten(hand, incoming, out output, maxShanten);
  }
}
