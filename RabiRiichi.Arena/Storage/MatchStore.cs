using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RabiRiichi.Arena.Storage {
  /// <summary>
  /// Comprehensive per-seat Mahjong and operational telemetry for a finished match.
  /// </summary>
  public sealed class MatchPlayer {
    public int Seat { get; set; }
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Provider { get; set; } = "";
    public int FinalPoints { get; set; }
    public int Placement { get; set; }
    public double EloBefore { get; set; }
    public double EloAfter { get; set; }

    // Mahjong gameplay metrics
    public int RoundsPlayed { get; set; }
    public int AgariCount { get; set; }
    public int TsumoCount { get; set; }
    public int RonCount { get; set; }
    public int DealInCount { get; set; }
    public int DealInPoints { get; set; }
    public int TotalWinPoints { get; set; }
    public int TotalWinHan { get; set; }
    public int MaxWinPoints { get; set; }
    public int RiichiCount { get; set; }
    public int OpenHandCount { get; set; }
    public int CallCount { get; set; }
    public int KanCount { get; set; }
    public int NukiCount { get; set; }
    public int TenpaiAtDrawCount { get; set; }
    public int WildcardWinCount { get; set; }
    public Dictionary<string, int> YakuCounts { get; set; } = new();

    // LLM operational metrics
    public int DecisionsCount { get; set; }
    public int AutoDecisionsCount { get; set; }
    public int ValidFirstTryCount { get; set; }
    public int RetryCount { get; set; }
    public int PenaltyCount { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long ReasoningTokens { get; set; }
    public long TotalLatencyMs { get; set; }
    public long MaxLatencyMs { get; set; }
  }

  /// <summary>
  /// Per-hand / per-kyoku outcome summary inside a match.
  /// </summary>
  public sealed class MatchRoundSummary {
    public int RoundIndex { get; set; }
    public string Label { get; set; } = "";
    /// <summary> "tsumo" | "ron" | "exhaustive_draw" | "abortive_draw". </summary>
    public string Outcome { get; set; } = "";
    public string AbortReason { get; set; }
    public List<int> WinnerSeats { get; set; } = new();
    public int? LoserSeat { get; set; }
    public List<int> TenpaiSeats { get; set; } = new();
    public List<string> Yaku { get; set; } = new();
    public int Han { get; set; }
    public int Fu { get; set; }
    public int Yakuman { get; set; }
    public int PointsWon { get; set; }
    public bool UsedWildcard { get; set; }
    public List<int> ScoreDeltas { get; set; } = new();
  }

  /// <summary>
  /// Full finished-match record persisted to <c>{runDir}/matches/{matchId}.json</c>.
  /// </summary>
  public sealed class MatchRecord {
    public string MatchId { get; set; } = "";
    public string GameId { get; set; } = "";
    public string RunId { get; set; } = "";
    public int SwissRound { get; set; }
    public string BatchLabel { get; set; } = "";
    public string StartedAt { get; set; } = "";
    public string FinishedAt { get; set; } = "";
    public long DurationMs { get; set; }
    public long Seed { get; set; }

    public int PlayerCount { get; set; } = 4;
    public int TotalRound { get; set; } = 1;
    public string TileSetPreset { get; set; } = "Regular";
    public bool HasWildcards { get; set; }
    public int WildcardCount { get; set; }
    public int RoundsPlayed { get; set; }
    public int ExhaustiveDraws { get; set; }
    public int AbortedDraws { get; set; }

    public JsonNode Config { get; set; }
    public List<MatchPlayer> Players { get; set; } = new();
    public List<MatchRoundSummary> Rounds { get; set; } = new();
  }

  public sealed class MatchIndexEntry {
    public string MatchId { get; set; } = "";
    public string GameId { get; set; } = "";
    public string RunId { get; set; } = "";
    public int SwissRound { get; set; }
    public string BatchLabel { get; set; } = "";
    public string FinishedAt { get; set; } = "";
    public long DurationMs { get; set; }
    public int PlayerCount { get; set; } = 4;
    public int TotalRound { get; set; } = 1;
    public string TileSetPreset { get; set; } = "Regular";
    public bool HasWildcards { get; set; }
    public int RoundsPlayed { get; set; }
    public List<MatchIndexPlayer> Players { get; set; } = new();
  }

  public sealed class MatchIndexPlayer {
    public int Seat { get; set; }
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Provider { get; set; } = "";
    public int FinalPoints { get; set; }
    public int Placement { get; set; }
    public double EloAfter { get; set; }
    public double EloDelta { get; set; }
    public int AgariCount { get; set; }
    public int DealInCount { get; set; }
    public int PenaltyCount { get; set; }
  }

  public sealed class MatchPage {
    public IReadOnlyList<MatchIndexEntry> Items { get; set; } = new List<MatchIndexEntry>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
  }

  /// <summary>
  /// Flat-file match store rooted at <c>{runDir}/matches/</c>.
  /// </summary>
  public sealed class MatchStore {
    private static readonly JsonSerializerOptions JsonOptions = new() {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string matchesDir;
    private readonly string indexPath;
    private readonly object gate = new();
    private readonly List<MatchIndexEntry> index = new();

    public MatchStore(string workspaceDir) {
      matchesDir = Path.Combine(workspaceDir, "matches");
      indexPath = Path.Combine(matchesDir, "index.json");
      LoadIndex();
    }

    private void LoadIndex() {
      lock (gate) {
        index.Clear();
        if (!File.Exists(indexPath)) {
          return;
        }
        var json = File.ReadAllText(indexPath);
        if (string.IsNullOrWhiteSpace(json)) {
          return;
        }
        var list = JsonSerializer.Deserialize<List<MatchIndexEntry>>(json, JsonOptions)
            ?? new List<MatchIndexEntry>();
        index.AddRange(list);
      }
    }

    public void Append(MatchRecord record) {
      if (record == null || string.IsNullOrEmpty(record.MatchId)) {
        throw new ArgumentException("record.MatchId must not be empty", nameof(record));
      }
      if (!IsValidId(record.MatchId)) {
        throw new ArgumentException("Invalid matchId", nameof(record));
      }
      lock (gate) {
        Directory.CreateDirectory(matchesDir);
        var recordPath = Path.Combine(matchesDir, $"{record.MatchId}.json");
        AtomicFile.WriteAllText(recordPath, JsonSerializer.Serialize(record, JsonOptions));

        index.RemoveAll(e => e.MatchId == record.MatchId);
        index.Insert(0, Summarize(record));
        SaveIndexLocked();
      }
    }

    public MatchRecord Get(string matchId) {
      if (!IsValidId(matchId)) {
        return null;
      }
      lock (gate) {
        var recordPath = Path.Combine(matchesDir, $"{matchId}.json");
        if (!File.Exists(recordPath)) {
          return null;
        }
        var json = File.ReadAllText(recordPath);
        return JsonSerializer.Deserialize<MatchRecord>(json, JsonOptions);
      }
    }

    public IReadOnlyList<MatchRecord> GetAllRecordsChronological() {
      List<string> ids;
      lock (gate) {
        ids = index.Select(e => e.MatchId).Reverse().ToList();
      }
      var result = new List<MatchRecord>(ids.Count);
      foreach (var id in ids) {
        var rec = Get(id);
        if (rec != null) result.Add(rec);
      }
      return result;
    }

    public IReadOnlyList<MatchIndexEntry> ListAllIndex() {
      lock (gate) {
        return index.Select(Copy).ToList();
      }
    }

    public MatchPage List(int page, int pageSize, string modelId = null, string query = null) {
      if (page < 1) page = 1;
      if (pageSize < 1) pageSize = 1;
      lock (gate) {
        IEnumerable<MatchIndexEntry> filtered = index;
        if (!string.IsNullOrWhiteSpace(modelId)) {
          string m = modelId.Trim();
          filtered = filtered.Where(e =>
              e.Players.Any(p => string.Equals(p.ModelId, m, StringComparison.OrdinalIgnoreCase)));
        }
        if (!string.IsNullOrWhiteSpace(query)) {
          string q = query.Trim();
          filtered = filtered.Where(e =>
              e.MatchId.Contains(q, StringComparison.OrdinalIgnoreCase) ||
              (e.BatchLabel?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
              (e.TileSetPreset?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
              e.Players.Any(p =>
                  p.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                  p.ModelId.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }
        var materialized = filtered.ToList();
        int total = materialized.Count;
        var items = materialized
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Copy)
            .ToList();
        return new MatchPage {
          Items = items,
          Page = page,
          PageSize = pageSize,
          TotalCount = total,
        };
      }
    }

    private void SaveIndexLocked() {
      Directory.CreateDirectory(matchesDir);
      AtomicFile.WriteAllText(indexPath, JsonSerializer.Serialize(index, JsonOptions));
    }

    private static MatchIndexEntry Summarize(MatchRecord record) => new() {
      MatchId = record.MatchId,
      GameId = record.GameId,
      RunId = record.RunId,
      SwissRound = record.SwissRound,
      BatchLabel = record.BatchLabel ?? "",
      FinishedAt = record.FinishedAt,
      DurationMs = record.DurationMs,
      PlayerCount = record.PlayerCount,
      TotalRound = record.TotalRound,
      TileSetPreset = record.TileSetPreset ?? "Regular",
      HasWildcards = record.HasWildcards,
      RoundsPlayed = record.RoundsPlayed,
      Players = record.Players.Select(p => new MatchIndexPlayer {
        Seat = p.Seat,
        ModelId = p.ModelId,
        DisplayName = p.DisplayName,
        Provider = p.Provider,
        FinalPoints = p.FinalPoints,
        Placement = p.Placement,
        EloAfter = p.EloAfter,
        EloDelta = p.EloAfter - p.EloBefore,
        AgariCount = p.AgariCount,
        DealInCount = p.DealInCount,
        PenaltyCount = p.PenaltyCount,
      }).ToList(),
    };

    private static MatchIndexEntry Copy(MatchIndexEntry e) => new() {
      MatchId = e.MatchId,
      GameId = e.GameId,
      RunId = e.RunId,
      SwissRound = e.SwissRound,
      BatchLabel = e.BatchLabel,
      FinishedAt = e.FinishedAt,
      DurationMs = e.DurationMs,
      PlayerCount = e.PlayerCount,
      TotalRound = e.TotalRound,
      TileSetPreset = e.TileSetPreset,
      HasWildcards = e.HasWildcards,
      RoundsPlayed = e.RoundsPlayed,
      Players = e.Players.Select(p => new MatchIndexPlayer {
        Seat = p.Seat,
        ModelId = p.ModelId,
        DisplayName = p.DisplayName,
        Provider = p.Provider,
        FinalPoints = p.FinalPoints,
        Placement = p.Placement,
        EloAfter = p.EloAfter,
        EloDelta = p.EloDelta,
        AgariCount = p.AgariCount,
        DealInCount = p.DealInCount,
        PenaltyCount = p.PenaltyCount,
      }).ToList(),
    };

    public static bool IsValidId(string id) =>
        !string.IsNullOrEmpty(id) && id.All(c => char.IsLetterOrDigit(c) || c == '-');
  }
}
