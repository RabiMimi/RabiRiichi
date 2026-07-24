using System;
using System.Collections.Generic;
using System.Linq;
using RabiRiichi.Arena.Storage;

namespace RabiRiichi.Arena.Eval {
  /// <summary>
  /// One participant in a finished table, as seen by the rating math. See
  /// ARENA_DESIGN.md §11a.
  /// </summary>
  public sealed class RatingParticipant {
    /// <summary>Stable model id (the ratings/stats key).</summary>
    public string ModelId { get; init; } = "";

    /// <summary>Current (pre-match) Elo of this participant.</summary>
    public double Elo { get; init; }

    /// <summary>
    /// 1-based placement in the finished table (1 = winner). Ties are already
    /// broken deterministically by <c>EvalRoom</c> before reaching here, so
    /// placements are a permutation of 1..N; equal placements are still handled
    /// (0.5/0.5) for robustness.
    /// </summary>
    public int Placement { get; init; }

    /// <summary>
    /// A frozen anchor rating (baseline). When set, this participant's Elo is
    /// NEVER updated, but it still contributes to opponents' expected scores at
    /// this fixed rating (§11a). Non-baselines leave this null.
    /// </summary>
    public double? FrozenElo { get; init; }

    /// <summary>Penalties incurred this match (added to the counter on apply).</summary>
    public int PenaltyCount { get; init; }

    public bool IsFrozen => FrozenElo.HasValue;
  }

  /// <summary>
  /// Placement-based multiplayer Elo (ARENA_DESIGN.md §11a). Within a finished
  /// table, every unordered pair of players is a mini-match: the better
  /// placement "wins" (0.5/0.5 on a tie). Each player's Elo delta is the sum
  /// over its opponents of <c>K * (actual − expected)</c>, with the expected
  /// score from the standard logistic formula on the players' CURRENT Elos.
  ///
  /// Baselines are frozen anchors: a participant with a <see cref="RatingParticipant.FrozenElo"/>
  /// never has its Elo changed, but still influences opponents' expected scores
  /// at its fixed rating, keeping the scale stable.
  ///
  /// The pure math (<see cref="ComputeEloDeltas"/>) is separated from the
  /// store-mutating <see cref="ApplyMatch"/> so it is trivially unit-testable in
  /// isolation, with no I/O.
  /// </summary>
  public sealed class RatingService {
    private readonly double kFactor;
    private readonly double initialElo;

    public RatingService(ArenaConfig.RatingConfig rating) {
      rating ??= new ArenaConfig.RatingConfig();
      kFactor = rating.KFactor;
      initialElo = rating.InitialElo;
    }

    public RatingService(double kFactor, double initialElo) {
      this.kFactor = kFactor;
      this.initialElo = initialElo;
    }

    public double KFactor => kFactor;
    public double InitialElo => initialElo;

    /// <summary>
    /// The logistic expected score of a player rated <paramref name="ratingA"/>
    /// against an opponent rated <paramref name="ratingB"/>: the classic Elo
    /// win-probability, in [0, 1].
    /// </summary>
    public static double ExpectedScore(double ratingA, double ratingB) {
      return 1.0 / (1.0 + Math.Pow(10.0, (ratingB - ratingA) / 400.0));
    }

    /// <summary>
    /// The pairwise "actual" score for a player with placement
    /// <paramref name="placementA"/> against an opponent with
    /// <paramref name="placementB"/>: 1 if strictly better (smaller placement
    /// number), 0 if strictly worse, 0.5 on a tie.
    /// </summary>
    public static double PairwiseScore(int placementA, int placementB) {
      if (placementA < placementB) {
        return 1.0;
      }
      if (placementA > placementB) {
        return 0.0;
      }
      return 0.5;
    }

    /// <summary>
    /// Computes each participant's Elo delta for one finished table. Pure: no
    /// I/O, no mutation of the inputs. Frozen participants always get a delta of
    /// 0 (their rating is an anchor) but still contribute to others' expected
    /// scores. The result is keyed by <see cref="RatingParticipant.ModelId"/>;
    /// when the same model id appears on multiple seats (duplicated baselines),
    /// their deltas are summed under that id.
    /// </summary>
    public IReadOnlyDictionary<string, double> ComputeEloDeltas(
        IReadOnlyList<RatingParticipant> participants) {
      if (participants == null) {
        throw new ArgumentNullException(nameof(participants));
      }
      var deltas = new Dictionary<string, double>();
      foreach (var p in participants) {
        deltas.TryAdd(p.ModelId, 0.0);
      }

      for (int i = 0; i < participants.Count; i++) {
        var a = participants[i];
        if (a.IsFrozen) {
          continue; // Anchors never move; skip computing their (unused) delta.
        }
        double delta = 0.0;
        for (int j = 0; j < participants.Count; j++) {
          if (i == j) {
            continue;
          }
          var b = participants[j];
          double expected = ExpectedScore(a.Elo, b.Elo);
          double actual = PairwiseScore(a.Placement, b.Placement);
          delta += kFactor * (actual - expected);
        }
        deltas[a.ModelId] += delta;
      }
      return deltas;
    }

    /// <summary>
    /// Applies one finished table to the <paramref name="store"/>: updates Elo
    /// (non-frozen only), increments games / wins (placement 1) / placement
    /// counters / penalties, and persists. New models start at
    /// <see cref="InitialElo"/> (or their frozen anchor). Returns per-model
    /// before/after Elo so the caller can stamp the match record (§11).
    ///
    /// When a model id occupies multiple seats (duplicated baselines), all its
    /// seats' counters and deltas are folded into the single stored record.
    /// </summary>
    public IReadOnlyDictionary<string, EloChange> ApplyMatch(
        RatingStore store, IReadOnlyList<RatingParticipant> participants) {
      if (store == null) {
        throw new ArgumentNullException(nameof(store));
      }
      if (participants == null) {
        throw new ArgumentNullException(nameof(participants));
      }

      var deltas = ComputeEloDeltas(participants);
      var changes = new Dictionary<string, EloChange>();

      // Group by model id so duplicated seats fold into one record.
      foreach (var group in participants.GroupBy(p => p.ModelId)) {
        var modelId = group.Key;
        var seats = group.ToList();
        var record = store.Get(modelId) ?? new RatingRecord {
          ModelId = modelId,
          Elo = seats[0].FrozenElo ?? initialElo,
        };

        double eloBefore = record.Elo;
        bool frozen = seats.Any(s => s.IsFrozen);
        if (frozen) {
          // Keep the anchor pinned to its configured frozen value.
          record.Elo = seats.First(s => s.IsFrozen).FrozenElo.Value;
        } else {
          record.Elo = eloBefore + deltas.GetValueOrDefault(modelId, 0.0);
        }

        foreach (var seat in seats) {
          record.Games++;
          switch (seat.Placement) {
            case 1: record.Place1++; record.Wins++; break;
            case 2: record.Place2++; break;
            case 3: record.Place3++; break;
            case 4: record.Place4++; break;
          }
          record.Penalties += seat.PenaltyCount;
        }

        store.Update(record);
        changes[modelId] = new EloChange {
          ModelId = modelId,
          EloBefore = eloBefore,
          EloAfter = record.Elo,
        };
      }

      return changes;
    }
  }

  /// <summary>Before/after Elo for one model in an applied match.</summary>
  public sealed class EloChange {
    public string ModelId { get; init; } = "";
    public double EloBefore { get; init; }
    public double EloAfter { get; init; }
    public double Delta => EloAfter - EloBefore;
  }
}
