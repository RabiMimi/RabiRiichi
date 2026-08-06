using RabiRiichi.Core;
using RabiRiichi.Utils;
using System.Collections.Generic;
using System.Linq;
using System;

namespace RabiRiichi.Patterns {
  public class BaseShiisuuputa : BasePattern {
    /// <inheritdoc/>
    public override bool Resolve(Hand hand, GameTile incoming, out List<List<MenLike>> output) {
      output = null;
      if (hand.game != null && !hand.game.IsFirstJun) {
        return false;
      }
      if (hand.Count != (incoming == null ? Game.HAND_SIZE + 1 : Game.HAND_SIZE)) {
        return false;
      }
      var allGameTiles = GetTileGroups(hand, incoming, true).GetAllBuckets().SelectMany(b => b).ToList();
      if (allGameTiles.Count != 14) {
        return false;
      }
      var tiles = allGameTiles.Select(gt => gt.tile).ToList();
      if (!IsUnrelated(tiles)) {
        return false;
      }
      var ret = tiles.Select(t => MenLike.From(new[] { allGameTiles.First(gt => gt.tile.IsSame(t)) })).ToList();
      output = [ret];
      return true;
    }

    /// <inheritdoc/>
    public override int Shanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten = int.MaxValue) {
      if (!hand.game.IsFirstJun) {
        return Reject(out output);
      }
      var allGameTiles = GetTileGroups(hand, incoming, true).GetAllBuckets().SelectMany(b => b).ToList();
      var allTiles = allGameTiles.Select(gt => gt.tile).ToList();
      int maxUnrelated = MaxUnrelatedSubset(allTiles);
      int shanten = 14 - maxUnrelated - 1;
      if (shanten > maxShanten) {
        return Reject(out output);
      }
      if (ShouldComputeDiscard(hand, incoming)) {
        var subset = FindMaxUnrelatedSubset(allTiles);
        output = new Tiles();
        output.AddRange(allTiles.Where(t => !subset.Contains(t)).Distinct());
      } else {
        var subset = FindMaxUnrelatedSubset(allTiles);
        output = new Tiles();
        var candidates = Tiles.AllDistinct.Where(t => {
          var test = new List<Tile>(subset) { t };
          return IsUnrelated(test);
        });
        output.AddRange(candidates);
      }
      return shanten;
    }

    public static bool IsUnrelated(IReadOnlyList<Tile> tiles) {
      var bySuit = tiles.GroupBy(t => t.Suit);
      foreach (var g in bySuit) {
        if (g.Key == TileSuit.Z) {
          var values = g.Select(t => t.Val).ToList();
          if (values.Distinct().Count() != values.Count) {
            return false;
          }
        } else {
          var sortedNums = g.Select(t => (int)t.Num).OrderBy(n => n).ToList();
          for (int i = 1; i < sortedNums.Count; i++) {
            if (sortedNums[i] - sortedNums[i - 1] < 3) {
              return false;
            }
          }
        }
      }
      return true;
    }

    private static int MaxUnrelatedSubset(List<Tile> tiles) {
      return FindMaxUnrelatedSubset(tiles).Count;
    }

    private static List<Tile> FindMaxUnrelatedSubset(List<Tile> tiles) {
      List<Tile> best = [];
      void Backtrack(int start, List<Tile> current) {
        if (current.Count > best.Count) {
          best = [.. current];
        }
        for (int i = start; i < tiles.Count; i++) {
          var t = tiles[i];
          current.Add(t);
          if (IsUnrelated(current)) {
            Backtrack(i + 1, current);
          }
          current.RemoveAt(current.Count - 1);
        }
      }
      Backtrack(0, []);
      return best;
    }
  }
}
