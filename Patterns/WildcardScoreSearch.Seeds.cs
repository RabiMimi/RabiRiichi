using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Patterns.Compiled;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  internal sealed partial class WildcardScoreSearch {
    private void Seed(BasePattern family) {
      // Strong early incumbents for many-wildcard hands; every seed is still
      // checked against naturals and every legal incoming-tile role. The seeds
      // below hold at most five tile kinds, so a hand with more cannot use them.
      if (required.Count(n => n > 0) > 5) return;
      for (int pair = 0; pair < 34; pair++) SeedTarget(family, pair, [27, 28, 29, 30]);
      for (int i = 0; i < 34; i++) SeedTarget(family, i, [i, i, i, i]);
      for (int i = 34; i < 55; i++) SeedTarget(family, melds[i].TileIndex + 1, [i, i, i, i]);
      SeedHighValueTriplets(family);
      if ((hand.game.config.scoringOption & (ScoringOption.Yakuman | ScoringOption.MultipleYakuman)) == ScoringOption.Yakuman) {
        // With a single-yakuman cap, han breaks many ties. Repeated value
        // triplets with a suited pair establish a much stronger incumbent than
        // all-honor yakuman targets (whose yaku suppress several han awards).
        int honor = 27, bestWeight = -1;
        for (int i = 27; i < 34; i++) {
          int weight = 0;
          foreach (var rule in boundRules) weight += rule switch {
            YakuRule.YakuhaiBakaze when i == 27 + (int)hand.game.info.wind => 1,
            YakuRule.YakuhaiJikaze when i == 27 + (int)hand.player.Wind => 1,
            YakuRule.YakuhaiHaku when i == 31 => 1,
            YakuRule.YakuhaiHatsu when i == 32 => 1,
            YakuRule.YakuhaiChun when i == 33 => 1,
            _ => 0,
          };
          if (weight > bestWeight) { bestWeight = weight; honor = i; }
        }
        for (int pair = 0; pair < 34; pair++) {
          for (int i = 0; i < 34; i++) SeedTarget(family, pair, [i, i, i, i]);
          for (int other = 0; other < 34; other++) SeedTarget(family, pair, [honor, honor, honor, other]);
        }
      }
    }

    private readonly record struct TripletSeed(int Pair, int A, int B, int C, int D, YakuRule Rule);

    // Immutable templates are shared across searches; feasibility checks below avoid
    // constructing a target unless its free portion can contain every natural tile.
    private static readonly TripletSeed[] highValueTripletSeeds = BuildTripletSeeds();
    private static TripletSeed[] BuildTripletSeeds() {
      var result = new List<TripletSeed>();
      for (int pair = 0; pair < 34; pair++) {
        for (int fourth = 0; fourth < 34; fourth++)
          result.Add(new(pair, 31, 32, 33, fourth, YakuRule.Daisangen));
        for (int suit = 0; suit < 3; suit++) for (int rank = 0; rank < 6; rank++) {
          int first = suit * 9 + rank;
          result.Add(new(pair, first, first + 1, first + 2, first + 3, YakuRule.Suurenkou));
        }
      }
      for (int pair = 27; pair <= 30; pair++) for (int omitted = 27; omitted <= 30; omitted++) {
        var winds = Enumerable.Range(27, 4).Where(i => i != omitted).ToArray();
        for (int fourth = 0; fourth < 34; fourth++)
          result.Add(new(pair, winds[0], winds[1], winds[2], fourth, YakuRule.Shousuushii));
      }
      return result.ToArray();
    }

    private void SeedHighValueTriplets(BasePattern family) {
      // Unconstrained hands already get strong incumbents from the basic seeds.
      // These extra patterns are most valuable in uncapped scoring where their
      // yakuman and han can combine; other modes retain the existing fast path.
      if ((hand.game.config.scoringOption & ScoringOption.Yakuman) != 0 || !required.Any(n => n > 0)) return;
      bool dragons = boundRules.Contains(YakuRule.Daisangen);
      bool consecutive = boundRules.Contains(YakuRule.Suurenkou);
      bool winds = boundRules.Contains(YakuRule.Shousuushii);
      if (!dragons && !consecutive && !winds) return;
      foreach (var seed in highValueTripletSeeds) {
        if (seed.Rule == YakuRule.Daisangen && !dragons || seed.Rule == YakuRule.Suurenkou && !consecutive
            || seed.Rule == YakuRule.Shousuushii && !winds) continue;
        if (!SeedFeasible(seed)) continue;
        SeedTarget(family, seed.Pair, [seed.A, seed.B, seed.C, seed.D]);
      }
    }

    private bool SeedFeasible(TripletSeed seed) {
      int pair = seed.Pair;
      ReadOnlySpan<int> ids = stackalloc int[] { seed.A, seed.B, seed.C, seed.D };
      Span<bool> used = stackalloc bool[4];
      used.Clear();
      bool hasPair = false;
      foreach (var g in fixedGroups) {
        if (g.Kind == CompactMeldKind.Pair) {
          if (hasPair || g.TileIndex != pair) return false;
          hasPair = true;
        } else {
          if (g.Kind is not (CompactMeldKind.Triplet or CompactMeldKind.Quad)) return false;
          int match = -1;
          for (int i = 0; i < 4; i++) if (!used[i] && ids[i] == g.TileIndex) { match = i; break; }
          if (match < 0) return false;
          used[match] = true;
        }
      }
      for (int tile = 0; tile < 34; tile++) if (required[tile] > 0) {
        int available = !hasPair && pair == tile ? 2 : 0;
        for (int i = 0; i < 4; i++) if (!used[i] && ids[i] == tile) available += 3;
        if (available < required[tile]) return false;
      }
      return true;
    }

    private void SeedTarget(BasePattern family, int pair, int[] ids) {
      int start = count;
      var available = ids.Select(i => melds[i]).ToList();
      bool hasPair = false;
      foreach (var g in fixedGroups) {
        if (g.Kind == CompactMeldKind.Pair) { if (g.TileIndex != pair) return; hasPair = true; }
        else {
          int i = available.FindIndex(m => m.TileIndex == g.TileIndex && (m.Kind == g.Kind || g.Kind == CompactMeldKind.Quad && m.Kind == CompactMeldKind.Triplet));
          if (i < 0) return;
          available.RemoveAt(i);
        }
      }
      if (!hasPair) Push(new(CompactMeldKind.Pair, pair, true));
      foreach (var g in available) Push(g);
      if (Covered()) Visit(family, seed: true);
      while (count > start) Pop();
    }

  }
}
