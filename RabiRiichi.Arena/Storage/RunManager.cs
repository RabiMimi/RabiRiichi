using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Arena.Storage {
  /// <summary>
  /// A single self-contained run's on-disk bundle: a frozen config snapshot plus
  /// this run's own rating/match/run-state stores, all rooted at
  /// <c>{workspace}/runs/{runId}/</c> (ARENA_DESIGN.md §10/§11, multi-run
  /// extension). Runs are fully isolated from each other — editing the live
  /// <c>arena.config.json</c> after a run started never changes that run's
  /// snapshot, ratings, or matches.
  ///
  /// Replays (<c>replays/{gameId}.pb</c>) and reasoning transcripts remain GLOBAL
  /// (keyed by the globally-unique gameId), so the web client fetches a replay by
  /// gameId regardless of which run produced it.
  /// </summary>
  public sealed class RunContext {
    /// <summary>Stable run id (path-safe; also the run directory name).</summary>
    public string RunId { get; }

    /// <summary>Absolute path of this run's directory.</summary>
    public string Dir { get; }

    /// <summary>
    /// The config FROZEN at run creation (may contain secrets — never serve it to
    /// clients). Drives roster/rounds/rating for this run for its whole life.
    /// </summary>
    public ArenaConfig Config { get; }

    /// <summary>This run's own Elo/counters (<c>runs/{runId}/ratings.json</c>).</summary>
    public RatingStore Ratings { get; }

    /// <summary>This run's own match records (<c>runs/{runId}/matches/</c>).</summary>
    public MatchStore Matches { get; }

    /// <summary>This run's resumable state (<c>runs/{runId}/run.json</c>).</summary>
    public RunStateStore RunState { get; }

    public RunContext(string runId, string dir, ArenaConfig config) {
      RunId = runId;
      Dir = dir;
      Config = config ?? throw new ArgumentNullException(nameof(config));
      Ratings = new RatingStore(dir);
      Matches = new MatchStore(dir);
      RunState = new RunStateStore(dir);
    }
  }

  /// <summary>
  /// Lightweight, display-only summary of a run for the run selector / runs list
  /// (§12a/§12b). Derived on demand from the run's <c>run.json</c> + config
  /// snapshot; never carries secrets.
  /// </summary>
  public sealed class RunSummary {
    public string RunId { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string Status { get; set; } = RunStatus.Idle.ToString();
    public int SwissRounds { get; set; }
    public int CurrentSwissRound { get; set; }
    public int CompletedMatches { get; set; }

    /// <summary>Count of enabled roster entries in this run's snapshot.</summary>
    public int ModelCount { get; set; }
  }

  /// <summary>
  /// Owns the set of runs under <c>{workspace}/runs/</c> and the append-only
  /// creation-ordered <c>runs/index.json</c> (newest-first). Creates isolated
  /// runs (freezing a config snapshot), resolves a run's stores by id, and
  /// projects the run list. Thread-safe; run contexts are cached so repeated
  /// lookups share one set of stores (and thus one in-memory cache/lock).
  /// </summary>
  public sealed class RunManager {
    private static readonly JsonSerializerOptions JsonOptions = new() {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>One entry of <c>runs/index.json</c>: id + creation time.</summary>
    private sealed class RunIndexEntry {
      public string RunId { get; set; } = "";
      public string CreatedAt { get; set; } = "";
    }

    private readonly string runsDir;
    private readonly string indexPath;
    private readonly object gate = new();

    // Creation order, newest-first, mirrored to runs/index.json.
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

    /// <summary>
    /// Creates a brand-new run: mints an id, writes the frozen config snapshot to
    /// <c>runs/{runId}/config.json</c>, registers it as the newest run, and returns
    /// its context. The passed config is cloned so later edits to it never leak in.
    /// </summary>
    public RunContext CreateRun(ArenaConfig configToSnapshot) {
      if (configToSnapshot == null) {
        throw new ArgumentNullException(nameof(configToSnapshot));
      }
      lock (gate) {
        var runId = NewRunId();
        var dir = Path.Combine(runsDir, runId);
        Directory.CreateDirectory(dir);
        var snapshot = configToSnapshot.Clone();
        snapshot.Save(Path.Combine(dir, "config.json"));

        var ctx = new RunContext(runId, dir, snapshot);
        cache[runId] = ctx;
        order.Insert(0, new RunIndexEntry {
          RunId = runId,
          CreatedAt = DateTime.UtcNow.ToString("o"),
        });
        SaveIndexLocked();
        return ctx;
      }
    }

    /// <summary>Returns the run's context (stores + snapshot), or null if unknown.</summary>
    public RunContext GetRun(string runId) {
      if (!IsValidRunId(runId)) {
        return null;
      }
      lock (gate) {
        return GetRunLocked(runId);
      }
    }

    private RunContext GetRunLocked(string runId) {
      if (cache.TryGetValue(runId, out var existing)) {
        return existing;
      }
      var dir = Path.Combine(runsDir, runId);
      if (!Directory.Exists(dir)) {
        return null;
      }
      var cfg = ArenaConfig.Load(Path.Combine(dir, "config.json"));
      var ctx = new RunContext(runId, dir, cfg);
      cache[runId] = ctx;
      return ctx;
    }

    /// <summary>The newest run's id (creation order), or null if there are none.</summary>
    public string NewestRunId() {
      lock (gate) {
        return order.Count > 0 ? order[0].RunId : null;
      }
    }

    /// <summary>All run ids, newest-first.</summary>
    public IReadOnlyList<string> RunIds() {
      lock (gate) {
        return order.Select(e => e.RunId).ToList();
      }
    }

    /// <summary>Newest-first display summaries for the run selector / runs list.</summary>
    public IReadOnlyList<RunSummary> ListRuns() {
      lock (gate) {
        var summaries = new List<RunSummary>(order.Count);
        foreach (var entry in order) {
          var ctx = GetRunLocked(entry.RunId);
          if (ctx == null) {
            continue;
          }
          var state = ctx.RunState.Get();
          summaries.Add(new RunSummary {
            RunId = entry.RunId,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = state?.UpdatedAt ?? entry.CreatedAt,
            Status = (state?.Status ?? RunStatus.Idle).ToString(),
            SwissRounds = state?.SwissRounds ?? ctx.Config.Run.SwissRounds,
            CurrentSwissRound = state == null ? 0 : state.SwissRoundIndex + 1,
            CompletedMatches = state?.CompletedPairings.Count ?? 0,
            ModelCount = ctx.Config.Models.Count(m => m.Enabled),
          });
        }
        return summaries;
      }
    }

    private void SaveIndexLocked() {
      Directory.CreateDirectory(runsDir);
      AtomicFile.WriteAllText(indexPath, JsonSerializer.Serialize(order, JsonOptions));
    }

    /// <summary>Path-safety guard for run ids (alphanumerics and dashes).</summary>
    public static bool IsValidRunId(string id) =>
        !string.IsNullOrEmpty(id) && id.All(c => char.IsLetterOrDigit(c) || c == '-');

    private static string NewRunId() =>
        $"run-{DateTime.UtcNow:yyyyMMdd'T'HHmmss}-{Guid.NewGuid():N}".Substring(0, 32);
  }
}
