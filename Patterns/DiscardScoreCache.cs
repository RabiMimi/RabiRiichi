using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns.Compiled;
using System;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
  /// <summary>
  /// Scratch storage for one discard inquiry. All holdings are WithFreeTiles
  /// copies of the same hand, with the same calls, tile count, rules and event
  /// context. Nothing here survives the inquiry or contains physical bindings.
  /// </summary>
  internal sealed class DiscardScoreCache(bool reuseScores) {
    private const int MaxScores = 16384, MaxWitnesses = 256;
    internal readonly record struct GroupKey(UInt128 Low, ulong High);
    internal readonly record struct ScoreKey(BasePattern Family, GroupKey Groups, int IncomingIndex,
        TileSource Source, DiscardReason? Reason, bool? RiichiDiscard, bool Tsumo, int Aka, int Dora, int Ura);
    internal sealed record Witness(BasePattern Family, CompactMeld[] Groups);
    private readonly Dictionary<ScoreKey, CompactScore> scores = new();
    private readonly HashSet<(BasePattern, GroupKey)> witnessKeys = new();
    internal readonly List<Witness> Witnesses = new();
    internal long Hits { get; private set; }
    internal long VisitedNodes, BoundCalculations, VisitedTargets, EvaluatedInterpretations;

    internal bool ReuseScores => reuseScores;

    // Twelve bits per group, including a marker for the winning group. Sorting
    // removes search-order differences while keeping called/closed distinctions.
    internal static GroupKey Key(ReadOnlySpan<CompactMeld> groups, int role = -1) {
      Span<ushort> codes = stackalloc ushort[groups.Length];
      for (int i = 0; i < groups.Length; i++) {
        var g = groups[i];
        codes[i] = (ushort)(g.TileIndex | ((int)g.Kind << 6) | (g.IsClose ? 1 << 9 : 0)
            | (g.IsCalled ? 1 << 10 : 0) | (i == role ? 1 << 11 : 0));
      }
      codes.Sort();
      UInt128 low = 0; ulong high = (ulong)groups.Length << 60;
      for (int i = 0; i < codes.Length; i++) {
        if (i < 10) low |= (UInt128)codes[i] << (i * 12);
        else high |= (ulong)codes[i] << ((i - 10) * 12);
      }
      return new(low, high);
    }

    internal bool TryGet(ScoreKey key, out CompactScore score) {
      if (scores.TryGetValue(key, out score)) { Hits++; return true; }
      return false;
    }

    internal void Add(ScoreKey key, CompactScore score) {
      if (scores.Count < MaxScores) scores.TryAdd(key, score);
    }

    internal void Remember(BasePattern family, CompactMeld[] groups) {
      if (Witnesses.Count >= MaxWitnesses || !witnessKeys.Add((family, Key(groups)))) return;
      Witnesses.Add(new(family, (CompactMeld[])groups.Clone()));
    }
  }
}
