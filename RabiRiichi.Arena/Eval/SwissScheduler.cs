using System;
using System.Collections.Generic;
using System.Linq;
using RabiRiichi.Arena.Storage;

namespace RabiRiichi.Arena.Eval {
  public sealed class SwissStanding {
    public ArenaConfig.ModelConfig Model { get; init; }
    public double Elo { get; init; }
    public int GamesPlayed { get; init; }
    public string ModelId => Model?.Id ?? "";
  }

  public sealed class SwissTable {
    public string Label { get; init; } = "";
    public IReadOnlyList<EvalSeatAssignment> Assignments { get; init; } =
        Array.Empty<EvalSeatAssignment>();
    public bool Padded { get; init; }
    public IReadOnlyList<string> ModelIds =>
        Assignments.Select(a => a.Model.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
  }

  /// <summary>
  /// Pure, deterministic table scheduler supporting:
  ///  1. Swiss tournament rounds (similar-standing, rematch-averse, baseline-padded).
  ///  2. Balanced match batches (equalizes games played across all enabled models while
  ///     minimizing repeated pairings).
  ///  3. Explicit custom seat lineups.
  /// </summary>
  public sealed class SwissScheduler {
    private readonly int playerCount;

    public SwissScheduler(int playerCount = 4) {
      if (playerCount < 2 || playerCount > 4) {
        if (playerCount < 1) throw new ArgumentOutOfRangeException(nameof(playerCount));
      }
      this.playerCount = playerCount;
    }

    public IReadOnlyList<SwissTable> Pair(
        IReadOnlyList<SwissStanding> standings,
        IReadOnlyCollection<IReadOnlyCollection<string>> priorPairings,
        IReadOnlyList<ArenaConfig.ModelConfig> fillerPool) {
      if (standings == null) throw new ArgumentNullException(nameof(standings));
      if (standings.Count == 0) return Array.Empty<SwissTable>();

      var met = BuildMetSet(priorPairings);
      var sorted = standings
          .OrderByDescending(s => s.Elo)
          .ThenBy(s => s.ModelId, StringComparer.Ordinal)
          .ToList();

      var used = new bool[sorted.Count];
      var rawTables = new List<List<SwissStanding>>();

      for (int i = 0; i < sorted.Count; i++) {
        if (used[i]) continue;
        var table = new List<SwissStanding> { sorted[i] };
        used[i] = true;

        while (table.Count < playerCount) {
          int pick = PickNext(sorted, used, table, met);
          if (pick < 0) break;
          used[pick] = true;
          table.Add(sorted[pick]);
        }
        rawTables.Add(table);
      }

      var result = new List<SwissTable>(rawTables.Count);
      foreach (var table in rawTables) {
        bool padded = table.Count < playerCount;
        var models = table.Select(s => s.Model).ToList();
        if (padded) {
          AppendFillers(models, fillerPool);
        }
        var assignments = models
            .Select((m, seat) => new EvalSeatAssignment { Seat = seat, Model = m })
            .ToList();
        result.Add(new SwissTable { Assignments = assignments, Padded = padded });
      }
      return result;
    }

    /// <summary>
    /// Plans a single next balanced match that prioritizes models with the fewest games
    /// played so far while minimizing pairwise rematches.
    /// </summary>
    public SwissTable PlanNextBalancedTable(
        IReadOnlyList<SwissStanding> standings,
        IReadOnlyCollection<IReadOnlyCollection<string>> priorPairings,
        IReadOnlyList<ArenaConfig.ModelConfig> fillerPool,
        int matchIndex = 0) {
      if (standings == null || standings.Count == 0) {
        return null;
      }

      var pairCounts = BuildPairCountMap(priorPairings);
      var ordered = standings
          .OrderBy(s => s.GamesPlayed)
          .ThenByDescending(s => !s.Model.IsBaseline) // Prioritize non-baseline eval models first
          .ThenBy(s => ((s.ModelId.GetHashCode() & 0x7fffffff) + matchIndex * 7919) % 10007)
          .ThenBy(s => s.ModelId, StringComparer.Ordinal)
          .ToList();

      var chosen = new List<ArenaConfig.ModelConfig> { ordered[0].Model };
      var usedIdx = new HashSet<int> { 0 };

      while (chosen.Count < playerCount && usedIdx.Count < ordered.Count) {
        int bestIdx = -1;
        long bestScore = long.MaxValue;
        for (int i = 0; i < ordered.Count; i++) {
          if (usedIdx.Contains(i)) continue;
          var cand = ordered[i];
          long overlap = chosen.Sum(c => (long)pairCounts.GetValueOrDefault(PairKey(c.Id, cand.ModelId)));
          long score = (long)cand.GamesPlayed * 1000L + overlap * 100L + i;
          if (score < bestScore) {
            bestScore = score;
            bestIdx = i;
          }
        }
        if (bestIdx < 0) break;
        usedIdx.Add(bestIdx);
        chosen.Add(ordered[bestIdx].Model);
      }

      bool padded = chosen.Count < playerCount;
      if (padded) {
        AppendFillers(chosen, fillerPool.Count > 0 ? fillerPool : ordered.Select(o => o.Model).ToList());
      }

      var assignments = chosen
          .Select((m, seat) => new EvalSeatAssignment { Seat = seat, Model = m })
          .ToList();
      return new SwissTable {
        Label = $"Balanced #{matchIndex + 1}",
        Assignments = assignments,
        Padded = padded,
      };
    }

    private static int PickNext(
        List<SwissStanding> sorted, bool[] used, List<SwissStanding> table,
        HashSet<string> met) {
      int firstFree = -1;
      int bestNonRematch = -1;
      for (int j = 0; j < sorted.Count; j++) {
        if (used[j]) continue;
        if (firstFree < 0) firstFree = j;
        bool rematch = table.Any(t => met.Contains(PairKey(t.ModelId, sorted[j].ModelId)));
        if (!rematch) {
          bestNonRematch = j;
          break;
        }
      }
      return bestNonRematch >= 0 ? bestNonRematch : firstFree;
    }

    private void AppendFillers(
        List<ArenaConfig.ModelConfig> models,
        IReadOnlyList<ArenaConfig.ModelConfig> fillerPool) {
      if (fillerPool == null || fillerPool.Count == 0) {
        throw new InvalidOperationException(
            "Roster is smaller than playerCount and no filler model is available to pad the table.");
      }
      var pool = fillerPool
          .OrderByDescending(m => m.IsBaseline)
          .ThenByDescending(m => m.FrozenElo.HasValue)
          .ThenBy(m => m.Id, StringComparer.Ordinal)
          .ToList();
      int fi = 0;
      while (models.Count < playerCount) {
        models.Add(pool[fi % pool.Count]);
        fi++;
      }
    }

    private static HashSet<string> BuildMetSet(
        IReadOnlyCollection<IReadOnlyCollection<string>> priorPairings) {
      var met = new HashSet<string>(StringComparer.Ordinal);
      if (priorPairings == null) return met;
      foreach (var pairing in priorPairings) {
        var ids = pairing.Distinct().ToList();
        for (int a = 0; a < ids.Count; a++) {
          for (int b = a + 1; b < ids.Count; b++) {
            met.Add(PairKey(ids[a], ids[b]));
          }
        }
      }
      return met;
    }

    private static Dictionary<string, int> BuildPairCountMap(
        IReadOnlyCollection<IReadOnlyCollection<string>> priorPairings) {
      var counts = new Dictionary<string, int>(StringComparer.Ordinal);
      if (priorPairings == null) return counts;
      foreach (var pairing in priorPairings) {
        var ids = pairing.Distinct().ToList();
        for (int a = 0; a < ids.Count; a++) {
          for (int b = a + 1; b < ids.Count; b++) {
            var k = PairKey(ids[a], ids[b]);
            counts[k] = counts.GetValueOrDefault(k) + 1;
          }
        }
      }
      return counts;
    }

    private static string PairKey(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? $"{a}\u0000{b}" : $"{b}\u0000{a}";
  }
}
