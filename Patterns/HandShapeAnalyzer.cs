using RabiRiichi.Core;
using RabiRiichi.Patterns.Compiled;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary>Minimum missing natural tiles, with masks over the 34 natural identities.</summary>
  public readonly record struct ShapeAnalysis(int Deficit, ulong DrawMask, ulong DiscardMask);

  /// <summary>
  /// Structural distance to complete target hands. Universal wildcards fill one
  /// deficit each; they never introduce branches or a wildcard DP dimension.
  /// </summary>
  public static class HandShapeAnalyzer {
    public const int NaturalTileCount = 34;
    private const int Infinity = 100;
    private readonly record struct State(int A, int B, int Melds, int Pair);
    private readonly record struct Edge(int From, int To, int Required);
    private static readonly State[] states = (
        from m in Enumerable.Range(0, 5)
        from a in Enumerable.Range(0, m + 1)
        from b in Enumerable.Range(0, m - a + 1)
        from p in Enumerable.Range(0, 2)
        select new State(a, b, m, p)).ToArray();
    private static readonly Dictionary<State, int> indices = states
        .Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);
    private static readonly Edge[][] edges = [BuildEdges(true, false), BuildEdges(false, false), BuildEdges(false, true)];

    public static int ToIndex(Tile tile) {
      if (!tile.IsValid || tile.IsWildcard) throw new ArgumentException("Expected a natural tile", nameof(tile));
      return CompactMeld.Index(tile);
    }

    public static Tile FromIndex(int index) {
      if (index is < 0 or >= NaturalTileCount) throw new ArgumentOutOfRangeException(nameof(index));
      return CompactMeld.TileAt(index);
    }

    private static Edge[] BuildEdges(bool starts, bool honor) {
      var result = new List<Edge>();
      for (int i = 0; i < states.Length; i++) {
        var s = states[i];
        if (honor && (s.A != 0 || s.B != 0)) continue;
        for (int x = 0; x <= (starts ? 4 - s.Melds : 0); x++)
          for (int y = 0; y <= 4 - s.Melds - x; y++)
            for (int z = 0; z <= 1 - s.Pair; z++) {
              int next = indices[new(s.B, x, s.Melds + x + y, s.Pair + z)];
              result.Add(new(i, next, s.A + s.B + x + 3 * y + 2 * z));
            }
      }
      return result.ToArray();
    }

    public static ShapeAnalysis AnalyzeRegular(ReadOnlySpan<int> natural, int melds = 4, int pair = 1) {
      Validate(natural);
      if (melds is < 0 or > 4 || pair is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(melds));
      Span<int> prev = stackalloc int[70], next = stackalloc int[70];
      Span<ulong> draws = stackalloc ulong[70], nextDraws = stackalloc ulong[70];
      Span<ulong> discards = stackalloc ulong[70], nextDiscards = stackalloc ulong[70];
      prev.Fill(Infinity); draws.Clear(); discards.Clear();
      prev[indices[new(0, 0, 0, 0)]] = 0;
      for (int tile = 0; tile < NaturalTileCount; tile++) {
        next.Fill(Infinity); nextDraws.Clear(); nextDiscards.Clear();
        bool boundary = tile is 0 or 9 or 18 or 27;
        foreach (var edge in edges[tile >= 27 ? 2 : tile % 9 < 7 ? 0 : 1]) {
          var s = states[edge.From];
          if (boundary && (s.A != 0 || s.B != 0) || prev[edge.From] == Infinity) continue;
          int cost = prev[edge.From] + Math.Max(0, edge.Required - natural[tile]);
          if (cost > next[edge.To]) continue;
          ulong d = draws[edge.From] | (edge.Required > natural[tile] ? 1UL << tile : 0);
          ulong r = discards[edge.From] | (edge.Required < natural[tile] ? 1UL << tile : 0);
          if (cost < next[edge.To]) {
            next[edge.To] = cost; nextDraws[edge.To] = d; nextDiscards[edge.To] = r;
          } else { nextDraws[edge.To] |= d; nextDiscards[edge.To] |= r; }
        }
        var swap = prev; prev = next; next = swap;
        var ds = draws; draws = nextDraws; nextDraws = ds;
        var rs = discards; discards = nextDiscards; nextDiscards = rs;
      }
      int end = indices[new(0, 0, melds, pair)];
      return new(prev[end], draws[end], discards[end]);
    }

    private static void Validate(ReadOnlySpan<int> natural) {
      if (natural.Length != NaturalTileCount) throw new ArgumentException("Expected 34 natural counts", nameof(natural));
      foreach (int count in natural) if (count < 0 || count > 14) throw new ArgumentOutOfRangeException(nameof(natural));
    }

    public static bool HasWildcards(Hand hand, GameTile incoming) => incoming?.tile.IsWildcard == true
        || hand.freeTiles.Any(t => t.tile.IsWildcard);

    public static bool WildcardsEnabled(Hand hand) => hand.player?.game?.config?.HasWildcards == true;

    private static int[] Counts(Hand hand, GameTile incoming, out int wildcards) {
      var counts = new int[NaturalTileCount];
      int w = 0;
      void Add(GameTile t) { if (t.tile.IsWildcard) w++; else counts[ToIndex(t.tile)]++; }
      foreach (var t in hand.freeTiles) Add(t);
      if (incoming != null) Add(incoming);
      wildcards = w;
      return counts;
    }

    private static int Finish(Hand hand, GameTile incoming, int wildcards, ShapeAnalysis analysis,
        out Tiles output, int maxShanten) {
      int shanten = analysis.Deficit - wildcards - 1;
      if (shanten > maxShanten) { output = null; return int.MaxValue; }
      output = [];
      bool discard = BasePattern.ShouldComputeDiscard(hand, incoming);
      if (shanten == -1) return shanten;
      ulong mask = discard ? analysis.DiscardMask : analysis.DrawMask;
      for (int i = 0; i < NaturalTileCount; i++) if ((mask & (1UL << i)) != 0) output.Add(FromIndex(i));
      if (!discard && (wildcards > 0 || WildcardsEnabled(hand))) output.Add(Tile.Wildcard);
      return shanten;
    }

    public static int RegularShanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten) {
      int melds = 4, pair = 1;
      foreach (var group in hand.called) {
        if (group.Any(t => t.tile.IsWildcard)) return Reject(out output);
        if (group is Jantou) pair--; else if (group is Shun or Kou or Kan) melds--; else return Reject(out output);
      }
      if (melds < 0 || pair < 0) return Reject(out output);
      var counts = Counts(hand, incoming, out int w);
      return Finish(hand, incoming, w, AnalyzeRegular(counts, melds, pair), out output, maxShanten);
    }

    public static int SevenPairsShanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten) {
      var counts = Counts(hand, incoming, out int w);
      var locked = new bool[NaturalTileCount];
      foreach (var group in hand.called) {
        if (group is not Jantou || group.Any(t => t.tile.IsWildcard)) return Reject(out output);
        int i = ToIndex(group.First.tile);
        if (locked[i]) return Reject(out output);
        locked[i] = true;
      }
      int pairs = 7 - hand.called.Count;
      if (pairs < 0) return Reject(out output);
      // Distinct-pair selection; also propagates every tied optimal draw/discard.
      Span<int> dp = stackalloc int[8], next = stackalloc int[8];
      Span<ulong> d = stackalloc ulong[8], nd = stackalloc ulong[8], r = stackalloc ulong[8], nr = stackalloc ulong[8];
      dp.Fill(Infinity); d.Clear(); r.Clear(); dp[0] = 0;
      for (int i = 0; i < NaturalTileCount; i++) {
        next.Fill(Infinity); nd.Clear(); nr.Clear();
        for (int n = 0; n <= pairs; n++) for (int take = 0; take <= (locked[i] ? 0 : 1); take++) {
          if (n + take > pairs || dp[n] == Infinity) continue;
          int target = take * 2, cost = dp[n] + Math.Max(0, target - counts[i]);
          if (cost > next[n + take]) continue;
          ulong dm = d[n] | (target > counts[i] ? 1UL << i : 0);
          ulong rm = r[n] | (target < counts[i] ? 1UL << i : 0);
          if (cost < next[n + take]) { next[n + take] = cost; nd[n + take] = dm; nr[n + take] = rm; }
          else { nd[n + take] |= dm; nr[n + take] |= rm; }
        }
        var ds = dp; dp = next; next = ds;
        var dd = d; d = nd; nd = dd; var rr = r; r = nr; nr = rr;
      }
      return Finish(hand, incoming, w, new(dp[pairs], d[pairs], r[pairs]), out output, maxShanten);
    }

    public static int ThirteenOrphansShanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten) {
      if (hand.called.Count > 1 || hand.called.Any(g => g is not Jantou || !g.First.tile.Is19Z
          || g.Any(t => t.tile.IsWildcard))) return Reject(out output);
      var counts = Counts(hand, incoming, out int w);
      int best = Infinity; ulong draws = 0, discards = 0;
      for (int duplicate = 0; duplicate < NaturalTileCount; duplicate++) {
        if (!FromIndex(duplicate).Is19Z || hand.called.Count == 1 && ToIndex(hand.called[0].First.tile) != duplicate) continue;
        int cost = 0; ulong dm = 0, rm = 0;
        for (int i = 0; i < NaturalTileCount; i++) {
          int target = FromIndex(i).Is19Z ? (i == duplicate ? 2 : 1) : 0;
          if (hand.called.Count == 1 && i == duplicate) target -= 2;
          cost += Math.Max(0, target - counts[i]);
          if (target > counts[i]) dm |= 1UL << i;
          if (target < counts[i]) rm |= 1UL << i;
        }
        if (cost < best) { best = cost; draws = dm; discards = rm; }
        else if (cost == best) { draws |= dm; discards |= rm; }
      }
      return Finish(hand, incoming, w, new(best, draws, discards), out output, maxShanten);
    }

    private static int Reject(out Tiles output) { output = null; return int.MaxValue; }
  }
}
