using System;
using System.Collections.Generic;
using System.Linq;
using RabiRiichi.Arena.Config;
using RabiRiichi.Arena.Storage;

namespace RabiRiichi.Arena.Eval {
  public sealed class ArenaAnalyticsSummary {
    public string Scope { get; init; } = "current";
    public int TotalRuns { get; init; }
    public int TotalMatches { get; init; }
    public int TotalRounds { get; init; }
    public int ExhaustiveDraws { get; init; }
    public int AbortedDraws { get; init; }
    public int WildcardMatches { get; init; }
    public int WildcardWins { get; init; }
    public long TotalDecisions { get; init; }
    public double ValidFirstTryRate { get; init; }
    public long TotalPenalties { get; init; }
    public long TotalTokens { get; init; }
    public double AvgMatchDurationMs { get; init; }
  }

  public sealed class RichLeaderboardRow {
    public int Rank { get; set; }
    public string ModelId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Provider { get; init; } = "";
    public string Variant { get; init; } = "";
    public bool IsBaseline { get; init; }
    public bool IsFrozen { get; init; }
    public double Elo { get; init; }
    public double EloCi95 { get; init; }

    public int Games { get; init; }
    public int Wins { get; init; }
    public double WinRate { get; init; }
    public double Top2Rate { get; init; }
    public double FourthAvoidRate { get; init; }
    public double AvgPlacement { get; init; }
    public double AvgFinalPoints { get; init; }
    public int Place1 { get; init; }
    public int Place2 { get; init; }
    public int Place3 { get; init; }
    public int Place4 { get; init; }

    // Mahjong playstyle rates (per round played)
    public int RoundsPlayed { get; init; }
    public int AgariCount { get; init; }
    public double AgariRate { get; init; }
    public double TsumoShare { get; init; }
    public int DealInCount { get; init; }
    public double DealInRate { get; init; }
    public double RiichiRate { get; init; }
    public double OpenRate { get; init; }
    public double TenpaiDrawRate { get; init; }
    public double AvgWinPoints { get; init; }
    public double AvgDealInPoints { get; init; }
    public double AvgWinHan { get; init; }
    public int MaxWinPoints { get; init; }
    public int KanCount { get; init; }
    public int NukiCount { get; init; }
    public int WildcardWins { get; init; }

    // Operational LLM metrics
    public long Decisions { get; init; }
    public long AutoDecisions { get; init; }
    public double ValidFirstTryRate { get; init; }
    public long Retries { get; init; }
    public int Penalties { get; init; }
    public double AvgLatencyMs { get; init; }
    public long MaxLatencyMs { get; init; }
    public long PromptTokens { get; init; }
    public long CompletionTokens { get; init; }
    public long ReasoningTokens { get; init; }
    public double TokensPerGame { get; init; }
    public double TokensPerDecision { get; init; }
  }

  public sealed class EloHistoryPoint {
    public int MatchSeq { get; init; }
    public string MatchId { get; init; } = "";
    public string FinishedAt { get; init; } = "";
    public double Elo { get; init; }
    public int Placement { get; init; }
  }

  public sealed class ModelEloSeries {
    public string ModelId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Provider { get; init; } = "";
    public bool IsBaseline { get; init; }
    public IReadOnlyList<EloHistoryPoint> Points { get; init; } = Array.Empty<EloHistoryPoint>();
  }

  public sealed class HeadToHeadCell {
    public string ModelA { get; init; } = "";
    public string ModelB { get; init; } = "";
    public int SharedMatches { get; init; }
    public int ABeatB { get; init; }
    public int BBeatA { get; init; }
    public int Ties { get; init; }
    public double WinRateA { get; init; }
    public double AvgPointDiffA { get; init; }
  }

  public sealed class YakuStatItem {
    public string Name { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Group { get; init; } = "";
    public bool IsKoyaku { get; init; }
    public int Count { get; init; }
    public Dictionary<string, int> ByModel { get; init; } = new();
  }

  public sealed class ArenaAnalyticsPayload {
    public ArenaAnalyticsSummary Summary { get; init; } = new();
    public IReadOnlyList<RichLeaderboardRow> Leaderboard { get; init; } = Array.Empty<RichLeaderboardRow>();
    public IReadOnlyList<ModelEloSeries> EloSeries { get; init; } = Array.Empty<ModelEloSeries>();
    public IReadOnlyList<HeadToHeadCell> HeadToHead { get; init; } = Array.Empty<HeadToHeadCell>();
    public IReadOnlyList<YakuStatItem> YakuBreakdown { get; init; } = Array.Empty<YakuStatItem>();
  }

  /// <summary>
  /// Aggregates scoreboard rankings, Elo trajectories, pairwise head-to-head matrices,
  /// Mahjong playstyle distributions, yaku/koyaku frequencies, and LLM token/latency metrics
  /// for either a single run or across all runs (<c>scope = "all"</c>).
  /// </summary>
  public sealed class ArenaAnalyticsService(RunManager runManager, ArenaConfig liveConfig) {
    private readonly RunManager runManager = runManager;
    private readonly ArenaConfig liveConfig = liveConfig;

    public ArenaAnalyticsPayload Build(string scope = null, string activeRunId = null) {
      string normalized = string.IsNullOrWhiteSpace(scope) || scope == "current"
          ? (activeRunId ?? runManager.NewestRunId() ?? "")
          : scope.Trim();

      bool allRuns = string.Equals(normalized, "all", StringComparison.OrdinalIgnoreCase);
      var targetRuns = new List<RunContext>();
      if (allRuns) {
        targetRuns.AddRange(runManager.GetAllRunsChronological());
      } else if (!string.IsNullOrEmpty(normalized)) {
        var single = runManager.GetRun(normalized);
        if (single != null) targetRuns.Add(single);
      }

      // Collect model catalog across live config + run snapshots.
      var modelMeta = new Dictionary<string, ArenaConfig.ModelConfig>(StringComparer.Ordinal);
      foreach (var m in liveConfig.Models ?? []) {
        if (!string.IsNullOrEmpty(m.Id)) modelMeta[m.Id] = m;
      }
      foreach (var r in targetRuns) {
        foreach (var m in r.Config.Models ?? []) {
          if (!string.IsNullOrEmpty(m.Id) && !modelMeta.ContainsKey(m.Id)) {
            modelMeta[m.Id] = m;
          }
        }
      }

      var matches = new List<MatchRecord>();
      foreach (var r in targetRuns) {
        matches.AddRange(r.Matches.GetAllRecordsChronological());
      }
      matches = matches
          .OrderBy(m => m.FinishedAt, StringComparer.Ordinal)
          .ThenBy(m => m.MatchId, StringComparer.Ordinal)
          .ToList();

      var ratingCfg = (targetRuns.LastOrDefault()?.Config ?? liveConfig).Rating
          ?? new ArenaConfig.RatingConfig();

      // Recompute unified Elo across the selected scope when allRuns is true (or use stored ratings for single run).
      var unifiedRatings = new Dictionary<string, double>(StringComparer.Ordinal);
      var ratingSvc = new RatingService(ratingCfg);
      var eloSeriesMap = new Dictionary<string, List<EloHistoryPoint>>(StringComparer.Ordinal);

      var accByModel = new Dictionary<string, ModelAccumulator>(StringComparer.Ordinal);
      var h2hMap = new Dictionary<string, H2HAccumulator>(StringComparer.Ordinal);
      var yakuMap = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

      int totalRounds = 0, exhaustiveDraws = 0, abortedDraws = 0, wildcardMatches = 0, wildcardWins = 0;
      long totalDurationMs = 0;

      // Seed starting point (seq 0) for models that appear in matches.
      int seq = 0;
      foreach (var match in matches) {
        seq++;
        totalRounds += match.RoundsPlayed;
        exhaustiveDraws += match.ExhaustiveDraws;
        abortedDraws += match.AbortedDraws;
        totalDurationMs += match.DurationMs;
        if (match.HasWildcards) wildcardMatches++;

        Dictionary<string, double> postEloByModel;
        if (allRuns) {
          var parts = match.Players.Select(p => {
            modelMeta.TryGetValue(p.ModelId, out var mm);
            double? frozen = mm is { IsBaseline: true } ? mm.FrozenElo : null;
            double cur = unifiedRatings.TryGetValue(p.ModelId, out var e)
                ? e
                : (frozen ?? ratingCfg.InitialElo);
            return new RatingParticipant {
              ModelId = p.ModelId,
              Elo = cur,
              Placement = p.Placement,
              FrozenElo = frozen,
              PenaltyCount = p.PenaltyCount,
            };
          }).ToList();

          var deltas = ratingSvc.ComputeEloDeltas(parts);
          postEloByModel = new Dictionary<string, double>(StringComparer.Ordinal);
          foreach (var g in parts.GroupBy(x => x.ModelId)) {
            var first = g.First();
            double before = unifiedRatings.TryGetValue(g.Key, out var e)
                ? e
                : (first.FrozenElo ?? ratingCfg.InitialElo);
            double after = first.IsFrozen ? first.FrozenElo!.Value : before + deltas.GetValueOrDefault(g.Key);
            unifiedRatings[g.Key] = after;
            postEloByModel[g.Key] = after;
          }
        } else {
          postEloByModel = match.Players
              .GroupBy(p => p.ModelId, StringComparer.Ordinal)
              .ToDictionary(g => g.Key, g => g.Last().EloAfter, StringComparer.Ordinal);
        }

        // Accumulate per-seat stats + Elo trajectory.
        foreach (var p in match.Players) {
          if (string.IsNullOrEmpty(p.ModelId)) continue;
          if (!accByModel.TryGetValue(p.ModelId, out var acc)) {
            acc = new ModelAccumulator(p.ModelId, p.DisplayName, p.Provider);
            accByModel[p.ModelId] = acc;
          }
          acc.AddSeat(p, match.RoundsPlayed);
          wildcardWins += p.WildcardWinCount;

          if (p.YakuCounts != null) {
            foreach (var kv in p.YakuCounts) {
              if (!yakuMap.TryGetValue(kv.Key, out var perModel)) {
                perModel = new Dictionary<string, int>(StringComparer.Ordinal);
                yakuMap[kv.Key] = perModel;
              }
              perModel[p.ModelId] = perModel.GetValueOrDefault(p.ModelId) + kv.Value;
            }
          }
        }

        foreach (var g in match.Players.GroupBy(p => p.ModelId, StringComparer.Ordinal)) {
          if (string.IsNullOrEmpty(g.Key)) continue;
          if (!eloSeriesMap.TryGetValue(g.Key, out var pts)) {
            pts = new List<EloHistoryPoint>();
            modelMeta.TryGetValue(g.Key, out var mm);
            double startElo = (mm is { IsBaseline: true } ? mm.FrozenElo : null) ?? ratingCfg.InitialElo;
            pts.Add(new EloHistoryPoint {
              MatchSeq = 0,
              MatchId = "start",
              FinishedAt = match.StartedAt,
              Elo = startElo,
              Placement = 0,
            });
            eloSeriesMap[g.Key] = pts;
          }
          pts.Add(new EloHistoryPoint {
            MatchSeq = seq,
            MatchId = match.MatchId,
            FinishedAt = match.FinishedAt,
            Elo = postEloByModel.GetValueOrDefault(g.Key, ratingCfg.InitialElo),
            Placement = g.Min(x => x.Placement),
          });
        }

        // Pairwise head-to-head across distinct models at this table.
        var distinctSeats = match.Players
            .Where(p => !string.IsNullOrEmpty(p.ModelId))
            .ToList();
        for (int i = 0; i < distinctSeats.Count; i++) {
          for (int j = i + 1; j < distinctSeats.Count; j++) {
            var a = distinctSeats[i];
            var b = distinctSeats[j];
            if (a.ModelId == b.ModelId) continue;
            RecordPairwise(h2hMap, a, b);
            RecordPairwise(h2hMap, b, a);
          }
        }
      }

      // Also include any rated entries from single-run RatingStore even if match files were synthetic in unit tests.
      if (!allRuns && targetRuns.Count == 1) {
        foreach (var rr in targetRuns[0].Ratings.GetAll()) {
          if (!accByModel.TryGetValue(rr.ModelId, out var acc)) {
            modelMeta.TryGetValue(rr.ModelId, out var mm);
            acc = new ModelAccumulator(rr.ModelId, mm?.DisplayName ?? rr.ModelId, mm?.Provider ?? "");
            acc.SeedFromRatingRecord(rr);
            accByModel[rr.ModelId] = acc;
          }
          acc.LatestRatingElo = rr.Elo;
        }
      }

      var rows = accByModel.Values.Select(acc => {
        modelMeta.TryGetValue(acc.ModelId, out var mm);
        bool isBase = mm?.IsBaseline ?? string.Equals(acc.Provider, "baseline", StringComparison.OrdinalIgnoreCase);
        bool isFrozen = isBase && mm?.FrozenElo != null;
        double elo = allRuns
            ? unifiedRatings.GetValueOrDefault(acc.ModelId, mm?.FrozenElo ?? ratingCfg.InitialElo)
            : (acc.LatestRatingElo ?? acc.LastMatchElo ?? mm?.FrozenElo ?? ratingCfg.InitialElo);

        // Approximate 95% Elo confidence half-width (shrinks with sqrt(games)).
        double ci95 = isFrozen || acc.Games == 0
            ? 0.0
            : Math.Round(350.0 / Math.Sqrt(Math.Max(1, acc.Games)), 1);

        int rds = Math.Max(1, acc.RoundsPlayed);
        int gms = Math.Max(1, acc.Games);
        long decs = Math.Max(1L, acc.Decisions);
        long totalTok = acc.PromptTokens + acc.CompletionTokens;

        return new RichLeaderboardRow {
          ModelId = acc.ModelId,
          DisplayName = !string.IsNullOrWhiteSpace(mm?.DisplayName) ? mm.DisplayName : acc.DisplayName,
          Provider = !string.IsNullOrWhiteSpace(mm?.Provider) ? mm.Provider : acc.Provider,
          Variant = mm?.Variant ?? "",
          IsBaseline = isBase,
          IsFrozen = isFrozen,
          Elo = Math.Round(elo, 1),
          EloCi95 = ci95,
          Games = acc.Games,
          Wins = acc.Place1,
          WinRate = acc.Games > 0 ? (double)acc.Place1 / gms : 0,
          Top2Rate = acc.Games > 0 ? (double)(acc.Place1 + acc.Place2) / gms : 0,
          FourthAvoidRate = acc.Games > 0 ? 1.0 - ((double)acc.Place4 / gms) : 0,
          AvgPlacement = acc.Games > 0
              ? (acc.Place1 * 1.0 + acc.Place2 * 2.0 + acc.Place3 * 3.0 + acc.Place4 * 4.0) / gms
              : 0,
          AvgFinalPoints = acc.Games > 0 ? (double)acc.TotalFinalPoints / gms : 0,
          Place1 = acc.Place1,
          Place2 = acc.Place2,
          Place3 = acc.Place3,
          Place4 = acc.Place4,
          RoundsPlayed = acc.RoundsPlayed,
          AgariCount = acc.AgariCount,
          AgariRate = acc.RoundsPlayed > 0 ? (double)acc.AgariCount / rds : 0,
          TsumoShare = acc.AgariCount > 0 ? (double)acc.TsumoCount / acc.AgariCount : 0,
          DealInCount = acc.DealInCount,
          DealInRate = acc.RoundsPlayed > 0 ? (double)acc.DealInCount / rds : 0,
          RiichiRate = acc.RoundsPlayed > 0 ? (double)acc.RiichiCount / rds : 0,
          OpenRate = acc.RoundsPlayed > 0 ? (double)acc.OpenHandCount / rds : 0,
          TenpaiDrawRate = acc.RoundsPlayed > 0 ? (double)acc.TenpaiAtDrawCount / rds : 0,
          AvgWinPoints = acc.AgariCount > 0 ? (double)acc.TotalWinPoints / acc.AgariCount : 0,
          AvgDealInPoints = acc.DealInCount > 0 ? (double)acc.DealInPoints / acc.DealInCount : 0,
          AvgWinHan = acc.AgariCount > 0 ? (double)acc.TotalWinHan / acc.AgariCount : 0,
          MaxWinPoints = acc.MaxWinPoints,
          KanCount = acc.KanCount,
          NukiCount = acc.NukiCount,
          WildcardWins = acc.WildcardWins,
          Decisions = acc.Decisions,
          AutoDecisions = acc.AutoDecisions,
          ValidFirstTryRate = acc.Decisions > 0 ? (double)acc.ValidFirstTry / decs : 1.0,
          Retries = acc.Retries,
          Penalties = acc.Penalties,
          AvgLatencyMs = acc.Decisions > 0 ? (double)acc.TotalLatencyMs / decs : 0,
          MaxLatencyMs = acc.MaxLatencyMs,
          PromptTokens = acc.PromptTokens,
          CompletionTokens = acc.CompletionTokens,
          ReasoningTokens = acc.ReasoningTokens,
          TokensPerGame = acc.Games > 0 ? (double)totalTok / gms : 0,
          TokensPerDecision = acc.Decisions > 0 ? (double)totalTok / decs : 0,
        };
      })
      .OrderByDescending(r => r.Elo)
      .ThenBy(r => r.AvgPlacement > 0 ? r.AvgPlacement : 99)
      .ThenBy(r => r.ModelId, StringComparer.Ordinal)
      .ToList();

      for (int i = 0; i < rows.Count; i++) {
        rows[i].Rank = i + 1;
      }

      var series = eloSeriesMap.Select(kv => {
        modelMeta.TryGetValue(kv.Key, out var mm);
        return new ModelEloSeries {
          ModelId = kv.Key,
          DisplayName = mm?.DisplayName ?? kv.Key,
          Provider = mm?.Provider ?? "",
          IsBaseline = mm?.IsBaseline ?? false,
          Points = kv.Value,
        };
      }).OrderBy(s => s.DisplayName, StringComparer.Ordinal).ToList();

      var h2hList = h2hMap.Values.Select(h => new HeadToHeadCell {
        ModelA = h.ModelA,
        ModelB = h.ModelB,
        SharedMatches = h.SharedMatches,
        ABeatB = h.ABeatB,
        BBeatA = h.BBeatA,
        Ties = h.Ties,
        WinRateA = h.SharedMatches > 0 ? (h.ABeatB + 0.5 * h.Ties) / h.SharedMatches : 0,
        AvgPointDiffA = h.SharedMatches > 0 ? (double)h.PointDiffSum / h.SharedMatches : 0,
      }).ToList();

      var yakuList = yakuMap.Select(kv => {
        ArenaMetadata.YakuByName.TryGetValue(kv.Key, out var cat);
        int total = kv.Value.Values.Sum();
        return new YakuStatItem {
          Name = kv.Key,
          DisplayName = cat?.DisplayName ?? kv.Key,
          Group = cat?.Group ?? "other",
          IsKoyaku = cat?.Group == "koyaku",
          Count = total,
          ByModel = kv.Value,
        };
      }).OrderByDescending(y => y.Count).ThenBy(y => y.Name, StringComparer.Ordinal).ToList();

      long sumDecisions = rows.Sum(r => r.Decisions);
      long sumValidFirst = accByModel.Values.Sum(a => a.ValidFirstTry);
      long sumPenalties = rows.Sum(r => (long)r.Penalties);
      long sumTokens = rows.Sum(r => r.PromptTokens + r.CompletionTokens);

      return new ArenaAnalyticsPayload {
        Summary = new ArenaAnalyticsSummary {
          Scope = allRuns ? "all" : normalized,
          TotalRuns = targetRuns.Count,
          TotalMatches = matches.Count,
          TotalRounds = totalRounds,
          ExhaustiveDraws = exhaustiveDraws,
          AbortedDraws = abortedDraws,
          WildcardMatches = wildcardMatches,
          WildcardWins = wildcardWins,
          TotalDecisions = sumDecisions,
          ValidFirstTryRate = sumDecisions > 0 ? (double)sumValidFirst / sumDecisions : 1.0,
          TotalPenalties = sumPenalties,
          TotalTokens = sumTokens,
          AvgMatchDurationMs = matches.Count > 0 ? (double)totalDurationMs / matches.Count : 0,
        },
        Leaderboard = rows,
        EloSeries = series,
        HeadToHead = h2hList,
        YakuBreakdown = yakuList,
      };
    }

    private static void RecordPairwise(
        Dictionary<string, H2HAccumulator> map, MatchPlayer a, MatchPlayer b) {
      string key = $"{a.ModelId}\u0000{b.ModelId}";
      if (!map.TryGetValue(key, out var h)) {
        h = new H2HAccumulator { ModelA = a.ModelId, ModelB = b.ModelId };
        map[key] = h;
      }
      h.SharedMatches++;
      if (a.Placement < b.Placement) h.ABeatB++;
      else if (a.Placement > b.Placement) h.BBeatA++;
      else h.Ties++;
      h.PointDiffSum += (long)a.FinalPoints - b.FinalPoints;
    }

    private sealed class H2HAccumulator {
      public string ModelA = "";
      public string ModelB = "";
      public int SharedMatches;
      public int ABeatB;
      public int BBeatA;
      public int Ties;
      public long PointDiffSum;
    }

    private sealed class ModelAccumulator(string modelId, string displayName, string provider) {
      public readonly string ModelId = modelId;
      public string DisplayName = string.IsNullOrWhiteSpace(displayName) ? modelId : displayName;
      public string Provider = provider ?? "";
      public double? LatestRatingElo;
      public double? LastMatchElo;

      public int Games;
      public int Place1, Place2, Place3, Place4;
      public long TotalFinalPoints;
      public int RoundsPlayed;
      public int AgariCount, TsumoCount, RonCount, DealInCount;
      public long DealInPoints, TotalWinPoints, TotalWinHan;
      public int MaxWinPoints;
      public int RiichiCount, OpenHandCount, CallCount, KanCount, NukiCount, TenpaiAtDrawCount, WildcardWins;

      public long Decisions, AutoDecisions, ValidFirstTry, Retries;
      public int Penalties;
      public long PromptTokens, CompletionTokens, ReasoningTokens, TotalLatencyMs, MaxLatencyMs;

      public void AddSeat(MatchPlayer p, int matchRounds) {
        if (!string.IsNullOrWhiteSpace(p.DisplayName)) DisplayName = p.DisplayName;
        if (!string.IsNullOrWhiteSpace(p.Provider)) Provider = p.Provider;
        LastMatchElo = p.EloAfter;
        Games++;
        switch (p.Placement) {
          case 1: Place1++; break;
          case 2: Place2++; break;
          case 3: Place3++; break;
          case 4: Place4++; break;
        }
        TotalFinalPoints += p.FinalPoints;
        RoundsPlayed += p.RoundsPlayed > 0 ? p.RoundsPlayed : matchRounds;
        AgariCount += p.AgariCount;
        TsumoCount += p.TsumoCount;
        RonCount += p.RonCount;
        DealInCount += p.DealInCount;
        DealInPoints += p.DealInPoints;
        TotalWinPoints += p.TotalWinPoints;
        TotalWinHan += p.TotalWinHan;
        if (p.MaxWinPoints > MaxWinPoints) MaxWinPoints = p.MaxWinPoints;
        RiichiCount += p.RiichiCount;
        OpenHandCount += p.OpenHandCount;
        CallCount += p.CallCount;
        KanCount += p.KanCount;
        NukiCount += p.NukiCount;
        TenpaiAtDrawCount += p.TenpaiAtDrawCount;
        WildcardWins += p.WildcardWinCount;

        Decisions += p.DecisionsCount;
        AutoDecisions += p.AutoDecisionsCount;
        ValidFirstTry += p.ValidFirstTryCount;
        Retries += p.RetryCount;
        Penalties += p.PenaltyCount;
        PromptTokens += p.PromptTokens;
        CompletionTokens += p.CompletionTokens;
        ReasoningTokens += p.ReasoningTokens;
        TotalLatencyMs += p.TotalLatencyMs;
        if (p.MaxLatencyMs > MaxLatencyMs) MaxLatencyMs = p.MaxLatencyMs;
      }

      public void SeedFromRatingRecord(RatingRecord r) {
        Games = r.Games;
        Place1 = r.Place1;
        Place2 = r.Place2;
        Place3 = r.Place3;
        Place4 = r.Place4;
        Penalties = r.Penalties;
        LatestRatingElo = r.Elo;
      }
    }
  }
}
