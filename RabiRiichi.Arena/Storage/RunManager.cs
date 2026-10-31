using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RabiRiichi.Arena.Storage {
  public sealed class RunContext {
    public string RunId { get; }
    public string Label { get; set; } = "";
    public string CreatedAt { get; }
    public string Dir { get; }
    public ArenaConfig Config { get; }
    public RatingStore Ratings { get; }
    public MatchStore Matches { get; }
    public RunStateStore RunState { get; }

    public RunContext(string runId, string dir, ArenaConfig config, string createdAt = "", string label = "") {
      RunId = runId;
      Dir = dir;
      Config = config ?? throw new ArgumentNullException(nameof(config));
      CreatedAt = createdAt ?? "";
      Label = label ?? "";
      Ratings = new RatingStore(dir);
      Matches = new MatchStore(dir);
      RunState = new RunStateStore(dir);
    }
  }

  public sealed class RunSummary {
    public string RunId { get; set; } = "";
    public string Label { get; set; } = "";
    public string ScheduleMode { get; set; } = "swiss";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string Status { get; set; } = RunStatus.Idle.ToString();
    public int SwissRounds { get; set; }
    public int CurrentSwissRound { get; set; }
    public int TargetMatches { get; set; }
    public int QueuedCustomTables { get; set; }
    public int CompletedMatches { get; set; }
    public int ModelCount { get; set; }
    public int PlayerCount { get; set; }
    public int TotalRound { get; set; }
    public string TileSetPreset { get; set; } = "Regular";
    public bool HasWildcards { get; set; }
  }

  /// <summary>
  /// Manages isolated runs under <c>{workspace}/runs/{runId}/</c>.
  /// </summary>
  public sealed class RunManager {
    private static readonly JsonSerializerOptions JsonOptions = new() {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class RunIndexEntry {
      public string RunId { get; set; } = "";
      public string Label { get; set; } = "";
      public string CreatedAt { get; set; } = "";
    }

    private readonly string runsDir;
    private readonly string indexPath;
    private readonly object gate = new();
    private readonly List<RunIndexEntry> order = new();
    private readonly Dictionary<string, RunContext> cache = new();

    public RunManager(string workspaceDir) {
      runsDir = Path.Combine(workspaceDir, "runs");
      indexPath = Path.Combine(runsDir, "index.json");
      Directory.CreateDirectory(runsDir);
      LoadIndex();
    }

    private void LoadIndex() {
      lock (gate) {
        order.Clear();
        if (!File.Exists(indexPath)) {
          return;
        }
        var json = File.ReadAllText(indexPath);
        if (string.IsNullOrWhiteSpace(json)) {
          return;
        }
        var list = JsonSerializer.Deserialize<List<RunIndexEntry>>(json, JsonOptions)
            ?? new List<RunIndexEntry>();
        order.AddRange(list.Where(e => !string.IsNullOrEmpty(e.RunId)));
      }
    }

    public RunContext CreateRun(ArenaConfig configToSnapshot, string label = null) {
      if (configToSnapshot == null) {
        throw new ArgumentNullException(nameof(configToSnapshot));
      }
      lock (gate) {
        var runId = NewRunId();
        var createdAt = DateTime.UtcNow.ToString("o");
        var cleanLabel = (label ?? "").Trim();
        var dir = Path.Combine(runsDir, runId);
        Directory.CreateDirectory(dir);
        var snapshot = configToSnapshot.Clone();
        snapshot.Save(Path.Combine(dir, "config.json"));

        var ctx = new RunContext(runId, dir, snapshot, createdAt, cleanLabel);
        cache[runId] = ctx;
        order.Insert(0, new RunIndexEntry {
          RunId = runId,
          Label = cleanLabel,
          CreatedAt = createdAt,
        });
        SaveIndexLocked();
        return ctx;
      }
    }

    public RunContext GetRun(string runId) {
      if (!IsValidRunId(runId)) {
        return null;
      }
      lock (gate) {
        return GetRunLocked(runId);
      }
    }

    public IReadOnlyList<RunContext> GetAllRunsChronological() {
      lock (gate) {
        var list = new List<RunContext>(order.Count);
        for (int i = order.Count - 1; i >= 0; i--) {
          var ctx = GetRunLocked(order[i].RunId);
          if (ctx != null) list.Add(ctx);
        }
        return list;
      }
    }

    public bool DeleteRun(string runId) {
      if (!IsValidRunId(runId)) {
        return false;
      }
      lock (gate) {
        int removed = order.RemoveAll(e => e.RunId == runId);
        cache.Remove(runId);
        var dir = Path.Combine(runsDir, runId);
        if (Directory.Exists(dir)) {
          try {
            Directory.Delete(dir, recursive: true);
          } catch {
            // Best-effort cleanup of run directory.
          }
          removed = Math.Max(removed, 1);
        }
        if (removed > 0) {
          SaveIndexLocked();
          return true;
        }
        return false;
      }
    }

    private RunContext GetRunLocked(string runId) {
      if (cache.TryGetValue(runId, out var existing)) {
        return existing;
      }
      var dir = Path.Combine(runsDir, runId);
      var cfgPath = Path.Combine(dir, "config.json");
      if (!Directory.Exists(dir) || !File.Exists(cfgPath)) {
        return null;
      }
      var cfg = ArenaConfig.Load(cfgPath);
      var entry = order.FirstOrDefault(e => e.RunId == runId);
      var ctx = new RunContext(runId, dir, cfg, entry?.CreatedAt ?? "", entry?.Label ?? "");
      cache[runId] = ctx;
      return ctx;
    }

    public string NewestRunId() {
      lock (gate) {
        return order.Count > 0 ? order[0].RunId : null;
      }
    }

    public IReadOnlyList<string> RunIds() {
      lock (gate) {
        return order.Select(e => e.RunId).ToList();
      }
    }

    public IReadOnlyList<RunSummary> ListRuns() {
      lock (gate) {
        var summaries = new List<RunSummary>(order.Count);
        foreach (var entry in order) {
          var ctx = GetRunLocked(entry.RunId);
          if (ctx == null) continue;
          var state = ctx.RunState.Get();
          var tileSet = ctx.Config.GameRules?.TileSet ?? new ArenaConfig.TileSetConfig();
          var builtTiles = tileSet.BuildTiles();
          bool hasWild = builtTiles.Any(t => t.IsWildcard);
          string label = !string.IsNullOrWhiteSpace(state?.Label) ? state.Label : entry.Label;

          summaries.Add(new RunSummary {
            RunId = entry.RunId,
            Label = label ?? "",
            ScheduleMode = state?.ScheduleMode ?? ctx.Config.Run.ScheduleMode ?? "swiss",
            CreatedAt = entry.CreatedAt,
            UpdatedAt = state?.UpdatedAt ?? entry.CreatedAt,
            Status = (state?.Status ?? RunStatus.Idle).ToString(),
            SwissRounds = state?.SwissRounds ?? ctx.Config.Run.SwissRounds,
            CurrentSwissRound = state == null ? 0 : state.SwissRoundIndex + 1,
            TargetMatches = state?.TargetMatches ?? ctx.Config.Run.TargetMatches,
            QueuedCustomTables = state?.QueuedTables?.Count ?? 0,
            CompletedMatches = state?.CompletedPairings.Count ?? 0,
            ModelCount = ctx.Config.Models.Count(m => m.Enabled),
            PlayerCount = ctx.Config.GameRules?.PlayerCount ?? 4,
            TotalRound = ctx.Config.GameRules?.TotalRound ?? 1,
            TileSetPreset = tileSet.Preset ?? "Regular",
            HasWildcards = hasWild,
          });
        }
        return summaries;
      }
    }

    private void SaveIndexLocked() {
      Directory.CreateDirectory(runsDir);
      AtomicFile.WriteAllText(indexPath, JsonSerializer.Serialize(order, JsonOptions));
    }

    public static bool IsValidRunId(string id) =>
        !string.IsNullOrEmpty(id) && id.All(c => char.IsLetterOrDigit(c) || c == '-');

    private static string NewRunId() =>
        $"run-{DateTime.UtcNow:yyyyMMdd'T'HHmmss}-{Guid.NewGuid():N}"[..32];
  }
}
