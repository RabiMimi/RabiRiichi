using RabiRiichi.Core;
using RabiRiichi.Patterns.Compiled;
using System;
using System.Collections.Concurrent;
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
    private const ulong Orphans = TileMasks.Honor | TileMasks.Terminal;
    // Sub-targets one suit can supply: 0..4 melds, each with or without the pair.
    private const int SuitSlots = 10;
    private static int Slot(int melds, int pair) => melds * 2 + pair;
    /// <summary>
    /// Suit answers depend only on that suit's counts, and hands share most suits
    /// between calls. A bound keeps unusual custom hands from growing this forever.
    /// </summary>
    private const int MaxCachedSuits = 1 << 16;
    private static readonly ConcurrentDictionary<ulong, uint[]> suitTables = new();

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

    /// <summary>
    /// For each sub-target, the cheapest way to build it from one suit. Entries pack
    /// the cost and the rank masks of tied draws and discards into a single value.
    /// </summary>
    private static uint[] BuildSuitTable(ReadOnlySpan<int> counts, bool honor) {
      Span<int> prev = stackalloc int[70], next = stackalloc int[70];
      Span<uint> draws = stackalloc uint[70], nextDraws = stackalloc uint[70];
      Span<uint> discards = stackalloc uint[70], nextDiscards = stackalloc uint[70];
      prev.Fill(Infinity); draws.Clear(); discards.Clear();
      prev[indices[new(0, 0, 0, 0)]] = 0;
      for (int rank = 0; rank < counts.Length; rank++) {
        next.Fill(Infinity); nextDraws.Clear(); nextDiscards.Clear();
        foreach (var edge in edges[honor ? 2 : rank < 7 ? 0 : 1]) {
          if (prev[edge.From] == Infinity) continue;
          int cost = prev[edge.From] + Math.Max(0, edge.Required - counts[rank]);
          if (cost > next[edge.To]) continue;
          uint d = draws[edge.From] | (edge.Required > counts[rank] ? 1u << rank : 0);
          uint r = discards[edge.From] | (edge.Required < counts[rank] ? 1u << rank : 0);
          if (cost < next[edge.To]) {
            next[edge.To] = cost; nextDraws[edge.To] = d; nextDiscards[edge.To] = r;
          } else { nextDraws[edge.To] |= d; nextDiscards[edge.To] |= r; }
        }
        var swap = prev; prev = next; next = swap;
        var ds = draws; draws = nextDraws; nextDraws = ds;
        var rs = discards; discards = nextDiscards; nextDiscards = rs;
      }
      var table = new uint[SuitSlots];
      for (int m = 0; m <= 4; m++) for (int p = 0; p <= 1; p++) {
        int end = indices[new(0, 0, m, p)];
        table[Slot(m, p)] = (uint)prev[end] | (draws[end] << 8) | (discards[end] << 17);
      }
      return table;
    }

    private static uint[] SuitTable(ReadOnlySpan<int> counts, bool honor) {
      ulong key = honor ? 1UL << 36 : 0;
      for (int rank = 0; rank < counts.Length; rank++) key |= (ulong)counts[rank] << (rank * 4);
      if (suitTables.TryGetValue(key, out var table)) return table;
      table = BuildSuitTable(counts, honor);
      if (suitTables.Count >= MaxCachedSuits) suitTables.Clear();
      suitTables[key] = table;
      return table;
    }

    public static ShapeAnalysis AnalyzeRegular(ReadOnlySpan<int> natural, int melds = 4, int pair = 1) {
      Validate(natural);
      if (melds is < 0 or > 4 || pair is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(melds));
      Span<int> best = stackalloc int[SuitSlots], next = stackalloc int[SuitSlots];
      Span<ulong> draws = stackalloc ulong[SuitSlots], nextDraws = stackalloc ulong[SuitSlots];
      Span<ulong> discards = stackalloc ulong[SuitSlots], nextDiscards = stackalloc ulong[SuitSlots];
      best.Fill(Infinity); draws.Clear(); discards.Clear();
      best[0] = 0;
      for (int suit = 0; suit < 4; suit++) {
        int shift = suit * 9;
        var table = SuitTable(natural.Slice(shift, suit == 3 ? 7 : 9), suit == 3);
        next.Fill(Infinity); nextDraws.Clear(); nextDiscards.Clear();
        for (int m = 0; m <= melds; m++) for (int p = 0; p <= pair; p++) {
          int from = Slot(m, p);
          if (best[from] == Infinity) continue;
          for (int dm = 0; m + dm <= melds; dm++) for (int dp = 0; p + dp <= pair; dp++) {
            uint entry = table[Slot(dm, dp)];
            int to = Slot(m + dm, p + dp), cost = best[from] + (int)(entry & 255);
            if (cost > next[to]) continue;
            ulong d = draws[from] | ((ulong)(entry >> 8 & 511) << shift);
            ulong r = discards[from] | ((ulong)(entry >> 17 & 511) << shift);
            if (cost < next[to]) {
              next[to] = cost; nextDraws[to] = d; nextDiscards[to] = r;
            } else { nextDraws[to] |= d; nextDiscards[to] |= r; }
          }
        }
        var swap = best; best = next; next = swap;
        var ds = draws; draws = nextDraws; nextDraws = ds;
        var rs = discards; discards = nextDiscards; nextDiscards = rs;
      }
      int end = Slot(melds, pair);
      return new(best[end], draws[end], discards[end]);
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
      int fixedPair = hand.called.Count == 1 ? ToIndex(hand.called[0].First.tile) : -1;
      int best = Infinity; ulong draws = 0, discards = 0;
      for (int duplicate = 0; duplicate < NaturalTileCount; duplicate++) {
        if ((Orphans >> duplicate & 1) == 0 || fixedPair >= 0 && fixedPair != duplicate) continue;
        int cost = 0; ulong dm = 0, rm = 0;
        for (int i = 0; i < NaturalTileCount; i++) {
          int target = (Orphans >> i & 1) != 0 ? (i == duplicate ? 2 : 1) : 0;
          if (i == fixedPair) target -= 2;
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
