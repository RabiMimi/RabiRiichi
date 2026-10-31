using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Arena.Storage {
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public enum RunStatus {
    Idle,
    Running,
    Pausing,
    Paused,
    Stopped,
    Finished,
    Failed,
  }

  public sealed class CompletedPairing {
    public int SwissRound { get; set; }
    public string MatchId { get; set; } = "";
    public List<string> ModelIds { get; set; } = new();
  }

  public sealed class QueuedBatchTable {
    public string Label { get; set; } = "";
    public List<string> SeatModelIds { get; set; } = new();
  }

  /// <summary>
  /// Resumable per-run state persisted to <c>{runDir}/run.json</c>.
  /// Supports Swiss rounds, Balanced match targets, and queued custom/batch tables.
  /// </summary>
  public sealed class RunState {
    public string RunId { get; set; } = "";
    public string Label { get; set; } = "";
    /// <summary> "swiss" | "balanced" | "custom". </summary>
    public string ScheduleMode { get; set; } = "swiss";

    public int SwissRoundIndex { get; set; }
    public int SwissRounds { get; set; }
    public int TargetMatches { get; set; }
    public int CompletedTablesInRound { get; set; }

    public List<QueuedBatchTable> QueuedTables { get; set; } = new();
    public List<CompletedPairing> CompletedPairings { get; set; } = new();

    public RunStatus Status { get; set; } = RunStatus.Idle;
    public string LastError { get; set; }
    public string StartedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
  }

  public sealed class RunStateStore {
    private static readonly JsonSerializerOptions JsonOptions = new() {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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
          state.QueuedTables ??= new List<QueuedBatchTable>();
        }
      }
    }

    public RunState Get() {
      lock (gate) {
        return state == null ? null : Copy(state);
      }
    }

    public void Save(RunState newState) {
      if (newState == null) {
        throw new ArgumentNullException(nameof(newState));
      }
      lock (gate) {
        state = Copy(newState);
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
      }
    }

    public void Clear() {
      lock (gate) {
        state = null;
        if (File.Exists(path)) {
          try {
            File.Delete(path);
          } catch {
            // Best-effort cleanup.
          }
        }
      }
    }

    private static RunState Copy(RunState s) => new() {
      RunId = s.RunId,
      Label = s.Label ?? "",
      ScheduleMode = string.IsNullOrWhiteSpace(s.ScheduleMode) ? "swiss" : s.ScheduleMode,
      SwissRoundIndex = s.SwissRoundIndex,
      SwissRounds = s.SwissRounds,
      TargetMatches = s.TargetMatches,
      CompletedTablesInRound = s.CompletedTablesInRound,
      Status = s.Status,
      LastError = s.LastError,
      StartedAt = s.StartedAt,
      UpdatedAt = s.UpdatedAt,
      QueuedTables = (s.QueuedTables ?? []).Select(q => new QueuedBatchTable {
        Label = q.Label ?? "",
        SeatModelIds = new List<string>(q.SeatModelIds ?? []),
      }).ToList(),
      CompletedPairings = (s.CompletedPairings ?? []).Select(p => new CompletedPairing {
        SwissRound = p.SwissRound,
        MatchId = p.MatchId,
        ModelIds = new List<string>(p.ModelIds ?? []),
      }).ToList(),
    };
  }
}
