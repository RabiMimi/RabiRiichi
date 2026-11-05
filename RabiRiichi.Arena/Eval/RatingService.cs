using System;
using System.Collections.Generic;
using System.Linq;
using RabiRiichi.Arena.Storage;

namespace RabiRiichi.Arena.Eval {
  public sealed class RatingParticipant {
    public string ModelId { get; init; } = "";
    public double Elo { get; init; }
    public int Placement { get; init; }
    public double? FrozenElo { get; init; }
    public int PenaltyCount { get; init; }

    public bool IsFrozen => FrozenElo.HasValue;
  }

  /// <summary>
  /// Placement-based multiplayer Elo calculator with configurable K-factor,
  /// initial rating, logistic scale, and frozen baseline anchors.
  /// </summary>
  public sealed class RatingService {
    private readonly double kFactor;
    private readonly double initialElo;
    private readonly double scale;

    public RatingService(ArenaConfig.RatingConfig rating) {
      rating ??= new ArenaConfig.RatingConfig();
      kFactor = rating.KFactor;
      initialElo = rating.InitialElo;
      scale = rating.Scale > 0 ? rating.Scale : 400.0;
    }

    public RatingService(double kFactor, double initialElo, double scale = 400.0) {
      this.kFactor = kFactor;
      this.initialElo = initialElo;
      this.scale = scale > 0 ? scale : 400.0;
    }

    public double KFactor => kFactor;
    public double InitialElo => initialElo;
    public double Scale => scale;

    public static double ExpectedScore(double ratingA, double ratingB, double scale = 400.0) {
      double s = scale > 0 ? scale : 400.0;
      return 1.0 / (1.0 + Math.Pow(10.0, (ratingB - ratingA) / s));
    }

    public static double PairwiseScore(int placementA, int placementB) {
      if (placementA < placementB) return 1.0;
      if (placementA > placementB) return 0.0;
      return 0.5;
    }

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
          continue;
        }
        double delta = 0.0;
        for (int j = 0; j < participants.Count; j++) {
          if (i == j) continue;
          var b = participants[j];
          double expected = ExpectedScore(a.Elo, b.Elo, scale);
          double actual = PairwiseScore(a.Placement, b.Placement);
          delta += kFactor * (actual - expected);
        }
        deltas[a.ModelId] += delta;
      }
      return deltas;
    }

    public IReadOnlyDictionary<string, EloChange> ApplyMatch(
        RatingStore store, IReadOnlyList<RatingParticipant> participants) {
      if (store == null) throw new ArgumentNullException(nameof(store));
      if (participants == null) throw new ArgumentNullException(nameof(participants));

      var deltas = ComputeEloDeltas(participants);
      var changes = new Dictionary<string, EloChange>();

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
          record.Elo = seats.First(s => s.IsFrozen).FrozenElo!.Value;
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

  public sealed class EloChange {
    public string ModelId { get; init; } = "";
    public double EloBefore { get; init; }
    public double EloAfter { get; init; }
    public double Delta => EloAfter - EloBefore;
  }
}
