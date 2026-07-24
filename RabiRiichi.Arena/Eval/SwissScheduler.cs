using System;
using System.Collections.Generic;
using System.Linq;
using RabiRiichi.Arena.Storage;

namespace RabiRiichi.Arena.Eval {
  /// <summary>
  /// One player's standing going into a Swiss round: the roster entry plus its
  /// current Elo (from the <c>RatingStore</c>, or the initial/frozen value for
  /// unrated models). See ARENA_DESIGN.md §10.
  /// </summary>
  public sealed class SwissStanding {
    public ArenaConfig.ModelConfig Model { get; init; }
    public double Elo { get; init; }
    public string ModelId => Model?.Id ?? "";
  }

  /// <summary>
  /// A single planned table: the seat assignments plus a flag noting whether it
  /// was padded with extra baseline fillers (§10). Seats are 0..playerCount-1.
  /// </summary>
  public sealed class SwissTable {
    public IReadOnlyList<EvalSeatAssignment> Assignments { get; init; } =
        Array.Empty<EvalSeatAssignment>();

    /// <summary>True if one or more seats are baseline fillers (roster not a multiple of 4).</summary>
    public bool Padded { get; init; }

    /// <summary>Sorted, de-duplicated model ids at this table (for rematch tracking).</summary>
    public IReadOnlyList<string> ModelIds =>
        Assignments.Select(a => a.Model.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
  }

  /// <summary>
  /// Pure, deterministic Swiss pairing (ARENA_DESIGN.md §10). Given the enabled
  /// roster's current standings, it groups players into tables of
  /// <c>playerCount</c> (4) among players of similar standing, avoiding rematches
  /// where possible, and pads a short final table with ADDITIONAL rule-based
  /// baseline instances (never byes).
  ///
  /// This class is intentionally free of any I/O, clock, or async so the pairing
  /// rules are trivially unit-testable in isolation. <see cref="ArenaService"/>
  /// feeds it standings (read from the <c>RatingStore</c>) and consumes the
  /// resulting tables.
  ///
  /// Pairing algorithm (greedy, similar-standing, rematch-averse):
  ///  1. Sort players by Elo DESCENDING, then by model id (stable tie-break).
  ///  2. Walk the sorted list; for each still-unpaired top player, seed a new
  ///     table with it, then fill the remaining seats by scanning the rest of the
  ///     sorted list (nearest standing first) and picking, at each step, the
  ///     closest player that has NOT already shared a table with anyone currently
  ///     at the table. If every remaining candidate is a rematch, take the
  ///     nearest one anyway (rematch avoidance is best-effort, §10).
  ///  3. If the roster size is not a multiple of playerCount, the last table is
  ///     short; it is padded with baseline fillers.
  /// </summary>
  public sealed class SwissScheduler {
    private readonly int playerCount;

    public SwissScheduler(int playerCount = 4) {
      if (playerCount < 1) {
        throw new ArgumentOutOfRangeException(nameof(playerCount));
      }
      this.playerCount = playerCount;
    }

    /// <summary>
    /// Produces the tables for one Swiss round.
    /// </summary>
    /// <param name="standings">Current standings of the enabled roster.</param>
    /// <param name="priorPairings">
    /// Previously completed pairings (each a set of model ids that shared a
    /// table) used to avoid rematches. May be null/empty for round 1.
    /// </param>
    /// <param name="fillerPool">
    /// Baseline roster entries usable to pad a short table (§10). Must be
    /// non-empty when the roster size is not a multiple of playerCount.
    /// </param>
    public IReadOnlyList<SwissTable> Pair(
        IReadOnlyList<SwissStanding> standings,
        IReadOnlyCollection<IReadOnlyCollection<string>> priorPairings,
        IReadOnlyList<ArenaConfig.ModelConfig> fillerPool) {
      if (standings == null) {
        throw new ArgumentNullException(nameof(standings));
      }
      if (standings.Count == 0) {
        return Array.Empty<SwissTable>();
      }

      // Set of unordered-pairs (a|b) that have already met, for rematch scoring.
      var met = BuildMetSet(priorPairings);

      // Sort by Elo desc, then id asc for a fully deterministic order.
      var sorted = standings
          .OrderByDescending(s => s.Elo)
          .ThenBy(s => s.ModelId, StringComparer.Ordinal)
          .ToList();

      var used = new bool[sorted.Count];
      var rawTables = new List<List<SwissStanding>>();

      for (int i = 0; i < sorted.Count; i++) {
        if (used[i]) {
          continue;
        }
        var table = new List<SwissStanding> { sorted[i] };
        used[i] = true;

        // Fill remaining seats with the nearest non-rematch candidates.
        while (table.Count < playerCount) {
          int pick = PickNext(sorted, used, table, met);
          if (pick < 0) {
            break; // No more players available (short final table).
          }
          used[pick] = true;
          table.Add(sorted[pick]);
        }
        rawTables.Add(table);
      }

      // Convert to seat assignments, padding a short table with fillers.
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
    /// Picks the index (in <paramref name="sorted"/>) of the nearest-standing,
    /// still-unused player that has not met anyone at <paramref name="table"/>;
    /// falls back to the nearest available player if all are rematches. Returns
    /// -1 if none remain. "Nearest" walks outward from the table's current
    /// centroid position in the sorted list.
    /// </summary>
    private static int PickNext(
        List<SwissStanding> sorted, bool[] used, List<SwissStanding> table,
        HashSet<string> met) {
      int firstFree = -1;
      int bestNonRematch = -1;
      for (int j = 0; j < sorted.Count; j++) {
        if (used[j]) {
          continue;
        }
        if (firstFree < 0) {
          firstFree = j;
        }
        bool rematch = table.Any(t => met.Contains(PairKey(t.ModelId, sorted[j].ModelId)));
        if (!rematch) {
          bestNonRematch = j;
          break; // sorted order => this is the nearest non-rematch by standing.
        }
      }
      return bestNonRematch >= 0 ? bestNonRematch : firstFree;
    }

    private void AppendFillers(
        List<ArenaConfig.ModelConfig> models,
        IReadOnlyList<ArenaConfig.ModelConfig> fillerPool) {
      if (fillerPool == null || fillerPool.Count == 0) {
        throw new InvalidOperationException(
            "Roster is not a multiple of playerCount but no baseline filler is " +
            "available to pad the short table (§10).");
      }
      // Prefer a frozen baseline filler; cycle through the pool for variety.
      var pool = fillerPool
          .OrderByDescending(m => m.FrozenElo.HasValue)
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
      if (priorPairings == null) {
        return met;
      }
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

    private static string PairKey(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? $"{a}\u0000{b}" : $"{b}\u0000{a}";
  }
}
