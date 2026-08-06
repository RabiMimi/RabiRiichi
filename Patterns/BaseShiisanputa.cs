using RabiRiichi.Core;
using RabiRiichi.Utils;
using System.Collections.Generic;
using System.Linq;
using System;

namespace RabiRiichi.Patterns {
  public class BaseShiisanputa : BasePattern {
    /// <inheritdoc/>
    public override bool Resolve(Hand hand, GameTile incoming, out List<List<MenLike>> output) {
      output = null;
      if (!hand.game.IsFirstJun) {
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
      if (!IsShiisanputa(tiles, out var pairTile)) {
        return false;
      }
      
      var pairGameTiles = allGameTiles.Where(gt => gt.tile.IsSame(pairTile)).Take(2).ToList();
      var remainingGameTiles = allGameTiles.Where(gt => !pairGameTiles.Contains(gt)).ToList();
      
      var ret = new List<MenLike> { MenLike.From(pairGameTiles) };
      foreach (var gt in remainingGameTiles) {
        ret.Add(MenLike.From(new[] { gt }));
      }
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
      var uniqueTiles = allTiles.DistinctBy(t => t.NoDoraVal).ToList();
      var maxSubset = FindMaxUnrelatedSubset(uniqueTiles);
      
      bool hasPair = allTiles.GroupBy(t => t.NoDoraVal).Any(g => g.Count() >= 2);
      
      int count = maxSubset.Count;
      if (hasPair) {
        count += 1;
      }
      int shanten = 14 - count - 1;
      if (shanten > maxShanten) {
        return Reject(out output);
      }
      
      if (ShouldComputeDiscard(hand, incoming)) {
        output = new Tiles();
        output.AddRange(allTiles.Where(t => !maxSubset.Contains(t)).Distinct());
      } else {
        output = new Tiles();
        var candidates = Tiles.AllDistinct.Where(t => {
          var test = new List<Tile>(maxSubset) { t };
          return BaseShiisuuputa.IsUnrelated(test);
        });
        output.AddRange(candidates);
      }
      return shanten;
    }

    public static bool IsShiisanputa(IReadOnlyList<Tile> tiles, out Tile pairTile) {
      pairTile = Tile.Empty;
      var groupings = tiles.GroupBy(t => t.NoDoraVal).ToList();
      if (groupings.Count != 13) {
        return false;
      }
      var pairGroup = groupings.FirstOrDefault(g => g.Count() == 2);
      if (pairGroup == null) {
        return false;
      }
      pairTile = pairGroup.First();
      var uniqueTiles = groupings.Select(g => g.First()).ToList();
      return BaseShiisuuputa.IsUnrelated(uniqueTiles);
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
          if (BaseShiisuuputa.IsUnrelated(current)) {
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
