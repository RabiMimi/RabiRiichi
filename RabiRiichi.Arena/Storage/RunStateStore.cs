using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Arena.Storage {
  /// <summary>
  /// Lifecycle status of a run, surfaced to controllers/pages. See
  /// ARENA_DESIGN.md §10/§12a.
  /// </summary>
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public enum RunStatus {
    /// <summary>No run has been started (fresh workspace).</summary>
    Idle,

    /// <summary>A run is scheduled and a match is (or is about to be) playing.</summary>
    Running,

    /// <summary>Between matches, waiting out the configured cooldown.</summary>
    Cooldown,

    /// <summary>A run exists but is halted (admin Stop, not yet finished).</summary>
    Stopped,

    /// <summary>Every Swiss round of the run has completed.</summary>
    Finished,
  }

  /// <summary>
  /// One completed pairing recorded for rematch avoidance and resume. Holds the
  /// (sorted) set of model ids that shared a table, the Swiss round it belonged
  /// to, and the produced match id. See §10.
  /// </summary>
  public sealed class CompletedPairing {
    public int SwissRound { get; set; }
    public string MatchId { get; set; } = "";
    public List<string> ModelIds { get; set; } = new();
  }

  /// <summary>
  /// Resumable run state persisted to <c>{workspace}/run.json</c> (§10/§11). It
  /// captures enough to resume a run after a restart: the run id, which Swiss
  /// round is in progress, how many tables of that round are already done, the
  /// completed pairings (for rematch avoidance), and the current status.
  ///
  /// A standings snapshot is intentionally NOT stored here — standings are
  /// recomputed from the authoritative <c>RatingStore</c> on resume so the two
  /// files can never drift.
  /// </summary>
  public sealed class RunState {
    /// <summary>Stable id for the current run.</summary>
    public string RunId { get; set; } = "";

    /// <summary>0-based index of the Swiss round currently in progress.</summary>
    public int SwissRoundIndex { get; set; }

    /// <summary>Number of Swiss rounds the run targets (snapshot of config).</summary>
    public int SwissRounds { get; set; }

    /// <summary>
    /// Tables of the CURRENT round already finished, so a resume replays only the
    /// remaining tables of the in-progress round.
    /// </summary>
    public int CompletedTablesInRound { get; set; }

    /// <summary>All finished pairings across the run (rematch avoidance §10).</summary>
    public List<CompletedPairing> CompletedPairings { get; set; } = new();

    /// <summary>Current lifecycle status (§10/§12a).</summary>
    public RunStatus Status { get; set; } = RunStatus.Idle;

    public string StartedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
  }

  /// <summary>
  /// Loads/saves the single <c>{workspace}/run.json</c> resumable run-state file
  /// (§10/§11). Load on boot, cache in memory, write-through on change; all
  /// writes are atomic and guarded by a lock. Returns deep copies so callers
  /// cannot mutate the cache behind the lock.
  /// </summary>
  public sealed class RunStateStore {
    private static readonly JsonSerializerOptions JsonOptions = new() {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string path;
    private readonly object gate = new();
    private RunState state;

    public RunStateStore(string workspaceDir) {
      path = Path.Combine(workspaceDir, "run.json");
      Load();
    }

    private void Load() {
      lock (gate) {
        state = null;
        if (!File.Exists(path)) {
          return;
        }
        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json)) {
          return;
        }
        state = JsonSerializer.Deserialize<RunState>(json, JsonOptions);
        if (state != null) {
          state.CompletedPairings ??= new List<CompletedPairing>();
        }
      }
    }

    /// <summary>
    /// Returns a deep copy of the persisted run state, or null if none exists.
    /// </summary>
    public RunState Get() {
      lock (gate) {
        return state == null ? null : Copy(state);
      }
    }

    /// <summary>Persists <paramref name="newState"/> atomically (write-through).</summary>
    public void Save(RunState newState) {
      if (newState == null) {
        throw new ArgumentNullException(nameof(newState));
      }
      lock (gate) {
        state = Copy(newState);
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
      }
    }

    /// <summary>Deletes the persisted run state (clears the file + cache).</summary>
    public void Clear() {
      lock (gate) {
        state = null;
        if (File.Exists(path)) {
          try {
            File.Delete(path);
          } catch {
            // Best-effort; a stale file is overwritten on the next Save.
          }
        }
      }
    }

    private static RunState Copy(RunState s) => new() {
      RunId = s.RunId,
      SwissRoundIndex = s.SwissRoundIndex,
      SwissRounds = s.SwissRounds,
      CompletedTablesInRound = s.CompletedTablesInRound,
      Status = s.Status,
      StartedAt = s.StartedAt,
      UpdatedAt = s.UpdatedAt,
      CompletedPairings = s.CompletedPairings
          .Select(p => new CompletedPairing {
            SwissRound = p.SwissRound,
            MatchId = p.MatchId,
            ModelIds = new List<string>(p.ModelIds),
          }).ToList(),
    };
  }
}
