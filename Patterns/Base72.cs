using RabiRiichi.Core;
using System;
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
    public override int Shanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten = 13) {
      if (hand.called.Any(gr => gr is not Jantou)) {
        return Reject(out output);
      }

      var buckets = GetTileGroups(hand, incoming, true);
      var allBuckets = buckets.GetAllBuckets().ToArray();
      var existingPairs = allBuckets.Where(tiles => tiles.Count >= 2).ToArray();
      var singleTiles = allBuckets
          .Where(tiles => tiles.Count == 1)
          .Select(tiles => tiles[0].tile.WithoutDora)
          .ToArray();
      int requiredSingle = 7 - existingPairs.Length;
      int ret = Game.HAND_SIZE
          - (existingPairs.Length * 2)
          - Math.Min(requiredSingle, singleTiles.Length);
      if (ret > maxShanten) {
        return Reject(out output);
      }
      if (ShouldComputeDiscard(hand, incoming)) {
        // 14张，计算切牌
        var tiles = GetHand(hand.freeTiles, incoming);
        output = [.. tiles.Where(tile => {
          int cnt = buckets.GetBucket(tile).Count;
          return singleTiles.Length > requiredSingle ? cnt is > 2 or 1 : cnt > 2;
        }).Distinct()];
      } else {
        // 13张，计算进张
        if (singleTiles.Length >= requiredSingle) {
          output = [.. singleTiles];
        } else {
          output = Tiles.AllDistinct;
          foreach (var pair in existingPairs) {
            output.Remove(pair[0].tile.WithoutDora);
          }
        }
      }
      return ret;
    }
  }
}