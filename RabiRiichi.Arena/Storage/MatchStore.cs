using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RabiRiichi.Arena.Storage {
  /// <summary>
  /// Per-seat result inside a finished match. See ARENA_DESIGN.md §11.
  /// </summary>
  public sealed class MatchPlayer {
    public int Seat { get; set; }
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int FinalPoints { get; set; }

    /// <summary>1-based placement (1 = winner).</summary>
    public int Placement { get; set; }
    public int PenaltyCount { get; set; }
    public double EloBefore { get; set; }
    public double EloAfter { get; set; }
  }

  /// <summary>
  /// A finished-match record written once to
  /// <c>{workspace}/matches/{matchId}.json</c>. <see cref="Seed"/> is always the
  /// real, post-game value (stamped at end-of-game). <see cref="Config"/> holds
  /// the full GameConfig snapshot (opaque JSON so this store does not depend on
  /// the engine proto). See §11.
  /// </summary>
  public sealed class MatchRecord {
    public string MatchId { get; set; } = "";
    public string GameId { get; set; } = "";
    public string RunId { get; set; } = "";
    public int SwissRound { get; set; }
    public string StartedAt { get; set; } = "";
    public string FinishedAt { get; set; } = "";
    public long Seed { get; set; }

    /// <summary>Full GameConfig snapshot (same fields as the client info modal).</summary>
    public JsonNode Config { get; set; }

    public List<MatchPlayer> Players { get; set; } = new();
  }

  /// <summary>
  /// Compact index entry (a prepended summary in <c>matches/index.json</c>) so
  /// the public page paginates without reading every match file. See §11.
  /// The replay link is NOT stored — it is derived at read-time from the live
  /// clientUrl/wsUrl + gameId (§11/§12a), so it stays correct across host/port
  /// changes.
  /// </summary>
  public sealed class MatchIndexEntry {
    public string MatchId { get; set; } = "";
    public string GameId { get; set; } = "";
    public string FinishedAt { get; set; } = "";
    public List<MatchIndexPlayer> Players { get; set; } = new();
  }

  public sealed class MatchIndexPlayer {
    public string DisplayName { get; set; } = "";
    public int FinalPoints { get; set; }
    public int Placement { get; set; }
    public double EloAfter { get; set; }
    public double EloDelta { get; set; }
  }

  /// <summary>
  /// Result page for paginated match listing (newest-first).
  /// </summary>
  public sealed class MatchPage {
    public IReadOnlyList<MatchIndexEntry> Items { get; set; } = new List<MatchIndexEntry>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
  }

  /// <summary>
  /// Flat-file match store. Appends full records to
  /// <c>{workspace}/matches/{matchId}.json</c> and prepends a compact summary to
  /// a reverse-chronological <c>{workspace}/matches/index.json</c>. Writes are
  /// atomic and serialized under a lock; the index is cached in memory. See §11.
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

    // In-memory cache of the reverse-chronological index (newest first).
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

    /// <summary>
    /// Appends a finished match: writes its full record file and prepends a
    /// compact summary to the index. No-op-safe re-append: an existing matchId is
    /// replaced (record overwritten, index de-duplicated) to keep consistency.
    /// </summary>
    public void Append(MatchRecord record) {
      if (record == null || string.IsNullOrEmpty(record.MatchId)) {
        throw new ArgumentException("record.MatchId must not be empty", nameof(record));
      }
      if (!MatchStore.IsValidId(record.MatchId)) {
        throw new ArgumentException("Invalid matchId", nameof(record));
      }
      lock (gate) {
        Directory.CreateDirectory(matchesDir);
        var recordPath = Path.Combine(matchesDir, $"{record.MatchId}.json");
        AtomicFile.WriteAllText(recordPath, JsonSerializer.Serialize(record, JsonOptions));

        // Rebuild the summary and prepend it (dropping any prior entry for this id).
        index.RemoveAll(e => e.MatchId == record.MatchId);
        index.Insert(0, Summarize(record));
        SaveIndexLocked();
      }
    }

    /// <summary>Returns the full record for a match, or null if not found.</summary>
    public MatchRecord Get(string matchId) {
      if (!MatchStore.IsValidId(matchId)) {
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

    /// <summary>
    /// Returns a newest-first page of match summaries. <paramref name="page"/> is
    /// 1-based; out-of-range pages return empty items with the true total count.
    /// </summary>
    public MatchPage List(int page, int pageSize) {
      if (page < 1) page = 1;
      if (pageSize < 1) pageSize = 1;
      lock (gate) {
        int total = index.Count;
        var items = index
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
      FinishedAt = record.FinishedAt,
      Players = record.Players.Select(p => new MatchIndexPlayer {
        DisplayName = p.DisplayName,
        FinalPoints = p.FinalPoints,
        Placement = p.Placement,
        EloAfter = p.EloAfter,
        EloDelta = p.EloAfter - p.EloBefore,
      }).ToList(),
    };

    private static MatchIndexEntry Copy(MatchIndexEntry e) => new() {
      MatchId = e.MatchId,
      GameId = e.GameId,
      FinishedAt = e.FinishedAt,
      Players = e.Players.Select(p => new MatchIndexPlayer {
        DisplayName = p.DisplayName,
        FinalPoints = p.FinalPoints,
        Placement = p.Placement,
        EloAfter = p.EloAfter,
        EloDelta = p.EloDelta,
      }).ToList(),
    };

    /// <summary>Path-safety guard for match ids (alphanumerics and dashes).</summary>
    public static bool IsValidId(string id) =>
        !string.IsNullOrEmpty(id) && id.All(c => char.IsLetterOrDigit(c) || c == '-');
  }
}
