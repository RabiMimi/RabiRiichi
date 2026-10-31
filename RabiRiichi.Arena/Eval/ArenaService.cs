using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Utils;

namespace RabiRiichi.Arena.Eval {
  public sealed class StartRunOptions {
    public string Label { get; set; } = "";
    /// <summary> Optional override: "swiss" | "balanced" | "custom". </summary>
    public string ScheduleMode { get; set; }
    public int? SwissRounds { get; set; }
    public int? TargetMatches { get; set; }
    public int? MatchConcurrency { get; set; }
    public List<List<string>> CustomTables { get; set; }
  }

  public sealed class AppendBatchOptions {
    /// <summary> "swiss_rounds" | "balanced_matches" | "custom_lineup". </summary>
    public string Mode { get; set; } = "balanced_matches";
    public int Count { get; set; } = 1;
    public string Label { get; set; } = "";
    public List<string> SeatModelIds { get; set; } = new();
  }

  public sealed class ArenaStatus {
    public RunStatus Status { get; init; }
    public string RunId { get; init; } = "";
    public string Label { get; init; } = "";
    public string ScheduleMode { get; init; } = "swiss";
    public int CurrentSwissRound { get; init; }
    public int TotalSwissRounds { get; init; }
    public int CompletedTablesInRound { get; init; }
    public int TotalTablesInRound { get; init; }
    public int TargetMatches { get; init; }
    public int QueuedCustomTables { get; init; }
    public int CompletedMatches { get; init; }
    public int MatchConcurrency { get; init; }
    public string LastError { get; init; }
    public IReadOnlyList<LiveMatchProgress> ActiveMatches { get; init; } =
        Array.Empty<LiveMatchProgress>();
    public NextMatchPreview NextMatch { get; init; }
  }

  public sealed class NextMatchPreview {
    public int SwissRound { get; init; }
    public string Label { get; init; } = "";
    public bool Padded { get; init; }
    public IReadOnlyList<NextMatchSeat> Seats { get; init; } = Array.Empty<NextMatchSeat>();
  }

  public sealed class NextMatchSeat {
    public int Seat { get; init; }
    public string ModelId { get; init; } = "";
    public string DisplayName { get; init; } = "";
  }

  /// <summary>
  /// Full-lifecycle Arena tournament &amp; batch orchestrator supporting:
  ///  - Swiss rounds, Balanced match targets, and Custom table lineups
  ///  - Parallel table execution up to <c>MatchConcurrency</c>
  ///  - Start / Resume, New Run, Graceful Pause, Immediate Stop, Append Batch,
  ///    Run Switching, and Run Deletion
  ///  - Live per-table scoreboard &amp; round progress tracking
  /// </summary>
  public sealed class ArenaService {
    public delegate Task<EvalResult> TableRunner(
        SwissTable table,
        string gameId,
        CancellationToken cancellationToken,
        Action<LiveMatchProgress> onProgress);

    private readonly RunManager runManager;
    private readonly Func<ArenaConfig> currentConfig;
    private readonly TableRunner tableRunner;
    private readonly Func<ArenaConfig, EvalRoom> evalRoomFactory;
    private readonly Func<DateTime> clock;

    private readonly object gate = new();
    private CancellationTokenSource cts;
    private Task loopTask;
    private volatile bool pauseRequested;

    private RunContext activeCtx;
    private RunState liveState;
    private RatingService activeRating;
    private SwissScheduler activeSwiss;

    private RunStatus status = RunStatus.Idle;
    private string runId = "";
    private int totalTablesInRound;
    private NextMatchPreview nextMatch;
    private readonly Dictionary<string, LiveMatchProgress> activeMatches = new(StringComparer.Ordinal);

    public ArenaService(
        RunManager runManager,
        Func<ArenaConfig> currentConfig,
        TableRunner tableRunner = null,
        Func<ArenaConfig, EvalRoom> evalRoomFactory = null,
        Func<DateTime> clock = null) {
      this.runManager = runManager ?? throw new ArgumentNullException(nameof(runManager));
      this.currentConfig = currentConfig ?? throw new ArgumentNullException(nameof(currentConfig));
      this.tableRunner = tableRunner;
      this.evalRoomFactory = evalRoomFactory;
      this.clock = clock ?? (() => DateTime.UtcNow);

      ReflectNewestRunForDisplay();
    }

    /// <summary> Backwards-compatible constructor overload for fast synchronous tableRunner tests. </summary>
    public ArenaService(
        RunManager runManager,
        Func<ArenaConfig> currentConfig,
        Func<SwissTable, string, CancellationToken, Task<EvalResult>> simpleRunner,
        Func<DateTime> clock = null)
        : this(
            runManager,
            currentConfig,
            simpleRunner == null
                ? null
                : (t, gid, ct, _) => simpleRunner(t, gid, ct),
            evalRoomFactory: null,
            clock: clock) {
    }

    public string ActiveRunId {
      get { lock (gate) { return runId; } }
    }

    public void Start() => Resume();

    /// <summary>
    /// Starts or resumes the active (or newest resumable) run; creates a fresh run
    /// if no unfinished run exists.
    /// </summary>
    public void Resume() {
      lock (gate) {
        if (loopTask != null && !loopTask.IsCompleted) {
          pauseRequested = false;
          if (status == RunStatus.Pausing) {
            status = RunStatus.Running;
          }
          return;
        }
        var ctx = ResolveResumableOrNewLocked();
        BeginLoopLocked(ctx, options: null);
        Logger.Log($"[Arena] Run loop started/resumed for {ctx.RunId}.");
      }
    }

    public async Task StartNewRunAsync(StartRunOptions options = null) {
      await StopAsync();
      lock (gate) {
        var snapshot = currentConfig().Clone();
        if (options != null) {
          if (!string.IsNullOrWhiteSpace(options.ScheduleMode)) {
            snapshot.Run.ScheduleMode = options.ScheduleMode.Trim().ToLowerInvariant();
          }
          if (options.SwissRounds.HasValue && options.SwissRounds.Value >= 1) {
            snapshot.Run.SwissRounds = options.SwissRounds.Value;
          }
          if (options.TargetMatches.HasValue && options.TargetMatches.Value >= 1) {
            snapshot.Run.TargetMatches = options.TargetMatches.Value;
          }
          if (options.MatchConcurrency.HasValue && options.MatchConcurrency.Value >= 1) {
            snapshot.Run.MatchConcurrency = Math.Clamp(options.MatchConcurrency.Value, 1, 16);
          }
        }
        var ctx = runManager.CreateRun(snapshot, options?.Label);
        BeginLoopLocked(ctx, options);
        Logger.Log($"[Arena] Started new run {ctx.RunId} (mode={snapshot.Run.ScheduleMode}).");
      }
    }

    /// <summary>
    /// Graceful pause: stops launching new matches while allowing in-flight tables
    /// to finish and persist cleanly before transitioning to <see cref="RunStatus.Paused"/>.
    /// </summary>
    public void Pause() {
      lock (gate) {
        if (loopTask == null || loopTask.IsCompleted) {
          if (activeCtx != null && status is RunStatus.Stopped or RunStatus.Idle) {
            var st = activeCtx.RunState.Get();
            if (st != null && st.Status != RunStatus.Finished) {
              st.Status = RunStatus.Paused;
              st.UpdatedAt = clock().ToString("o");
              activeCtx.RunState.Save(st);
              liveState = st;
              status = RunStatus.Paused;
            }
          }
          return;
        }
        pauseRequested = true;
        status = activeMatches.Count > 0 ? RunStatus.Pausing : RunStatus.Paused;
        Logger.Log($"[Arena] Graceful pause requested for {runId} ({activeMatches.Count} active matches).");
      }
    }

    public void Stop() {
      lock (gate) {
        pauseRequested = false;
        if (cts == null) return;
        Logger.Log("[Arena] Immediate stop requested; cancelling in-flight matches.");
        try {
          cts.Cancel();
        } catch (ObjectDisposedException) {
        }
      }
    }

    public async Task StopAsync() {
      Task toAwait;
      lock (gate) {
        toAwait = loopTask;
      }
      Stop();
      if (toAwait != null) {
        try {
          await toAwait;
        } catch (OperationCanceledException) {
        }
      }
    }

    /// <summary>
    /// Appends Swiss rounds, balanced matches, or custom seat lineups to the active
    /// run (or creates one if none exists) and starts/resumes execution.
    /// </summary>
    public List<string> AppendBatch(AppendBatchOptions options) {
      if (options == null) return ["Batch options must not be null."];
      int count = Math.Clamp(options.Count, 1, 200);
      string mode = (options.Mode ?? "balanced_matches").Trim().ToLowerInvariant();

      lock (gate) {
        var ctx = activeCtx ?? (runManager.NewestRunId() is string nid ? runManager.GetRun(nid) : null)
            ?? runManager.CreateRun(currentConfig(), options.Label);
        activeCtx = ctx;
        runId = ctx.RunId;

        int playerCount = ctx.Config.GameRules?.PlayerCount ?? 4;
        var enabledById = ctx.Config.Models
            .Where(m => m.Enabled)
            .ToDictionary(m => m.Id, StringComparer.Ordinal);
        var allById = ctx.Config.Models
            .GroupBy(m => m.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        if (enabledById.Count == 0 && mode != "custom_lineup") {
          return ["At least one model must be enabled in this run's roster."];
        }

        List<string> validatedLineup = null;
        if (mode == "custom_lineup") {
          if (options.SeatModelIds == null || options.SeatModelIds.Count != playerCount) {
            return [$"Custom lineup requires exactly {playerCount} seat model IDs."];
          }
          foreach (var mid in options.SeatModelIds) {
            if (string.IsNullOrWhiteSpace(mid) || !allById.ContainsKey(mid)) {
              return [$"Unknown model id '{mid}' in custom lineup."];
            }
          }
          validatedLineup = options.SeatModelIds.ToList();
        } else if (mode is not ("swiss_rounds" or "balanced_matches")) {
          return [$"Unknown batch mode '{options.Mode}'."];
        }

        var st = (loopTask != null && !loopTask.IsCompleted && liveState != null)
            ? liveState
            : (ctx.RunState.Get() ?? CreateInitialState(ctx, null));

        switch (mode) {
          case "swiss_rounds":
            if (st.ScheduleMode != "swiss") {
              st.ScheduleMode = "swiss";
            }
            st.SwissRounds += count;
            break;

          case "balanced_matches":
            if (st.ScheduleMode == "balanced") {
              st.TargetMatches = Math.Max(st.CompletedPairings.Count, st.TargetMatches) + count;
            } else {
              for (int i = 0; i < count; i++) {
                st.QueuedTables.Add(new QueuedBatchTable {
                  Label = string.IsNullOrWhiteSpace(options.Label) ? $"Batch +{i + 1}" : options.Label.Trim(),
                  SeatModelIds = new List<string>(), // Auto-plan balanced table at execution time
                });
              }
            }
            break;

          case "custom_lineup":
            for (int i = 0; i < count; i++) {
              st.QueuedTables.Add(new QueuedBatchTable {
                Label = string.IsNullOrWhiteSpace(options.Label) ? $"Custom #{i + 1}" : options.Label.Trim(),
                SeatModelIds = new List<string>(validatedLineup),
              });
            }
            break;
        }

        if (st.Status == RunStatus.Finished) {
          st.Status = RunStatus.Paused;
        }
        st.UpdatedAt = clock().ToString("o");
        ctx.RunState.Save(st);
        liveState = st;

        if (loopTask == null || loopTask.IsCompleted) {
          BeginLoopLocked(ctx, options: null);
        } else {
          pauseRequested = false;
          status = RunStatus.Running;
        }
        return [];
      }
    }

    public bool ActivateRun(string targetRunId, out string error) {
      lock (gate) {
        if (loopTask != null && !loopTask.IsCompleted) {
          error = "Pause or stop the currently running evaluation before switching active runs.";
          return false;
        }
        var ctx = runManager.GetRun(targetRunId);
        if (ctx == null) {
          error = $"Run '{targetRunId}' was not found.";
          return false;
        }
        activeCtx = ctx;
        liveState = ctx.RunState.Get();
        runId = ctx.RunId;
        status = liveState?.Status ?? RunStatus.Idle;
        totalTablesInRound = 0;
        nextMatch = null;
        error = null;
        return true;
      }
    }

    public bool DeleteRun(string targetRunId, out string error) {
      lock (gate) {
        if (loopTask != null && !loopTask.IsCompleted && targetRunId == runId) {
          error = "Stop the active run before deleting it.";
          return false;
        }
        bool deleted = runManager.DeleteRun(targetRunId);
        if (!deleted) {
          error = $"Run '{targetRunId}' was not found.";
          return false;
        }
        if (targetRunId == runId) {
          activeCtx = null;
          liveState = null;
          runId = "";
          status = RunStatus.Idle;
          ReflectNewestRunForDisplay();
        }
        error = null;
        return true;
      }
    }

    public ArenaStatus GetStatus() {
      lock (gate) {
        var cfg = activeCtx?.Config ?? currentConfig();
        var st = liveState ?? activeCtx?.RunState.Get();
        return new ArenaStatus {
          Status = status,
          RunId = runId,
          Label = st?.Label ?? activeCtx?.Label ?? "",
          ScheduleMode = st?.ScheduleMode ?? cfg.Run?.ScheduleMode ?? "swiss",
          CurrentSwissRound = status == RunStatus.Idle || st == null ? 0 : st.SwissRoundIndex + 1,
          TotalSwissRounds = st?.SwissRounds ?? cfg.Run?.SwissRounds ?? 0,
          CompletedTablesInRound = st?.CompletedTablesInRound ?? 0,
          TotalTablesInRound = totalTablesInRound,
          TargetMatches = st?.TargetMatches ?? cfg.Run?.TargetMatches ?? 0,
          QueuedCustomTables = st?.QueuedTables?.Count ?? 0,
          CompletedMatches = st?.CompletedPairings?.Count ?? 0,
          MatchConcurrency = cfg.Run?.MatchConcurrency ?? 1,
          LastError = st?.LastError,
          ActiveMatches = activeMatches.Values
              .OrderBy(m => m.StartedAt, StringComparer.Ordinal)
              .ToList(),
          NextMatch = nextMatch,
        };
      }
    }

    public static ArenaStatus BuildStatusFromState(RunState state, ArenaConfig cfg) {
      if (state == null) {
        return new ArenaStatus {
          Status = RunStatus.Idle,
          ScheduleMode = cfg?.Run?.ScheduleMode ?? "swiss",
          TotalSwissRounds = cfg?.Run?.SwissRounds ?? 0,
          TargetMatches = cfg?.Run?.TargetMatches ?? 0,
          MatchConcurrency = cfg?.Run?.MatchConcurrency ?? 1,
        };
      }
      return new ArenaStatus {
        Status = state.Status,
        RunId = state.RunId,
        Label = state.Label ?? "",
        ScheduleMode = state.ScheduleMode ?? "swiss",
        CurrentSwissRound = state.Status == RunStatus.Idle ? 0 : state.SwissRoundIndex + 1,
        TotalSwissRounds = state.SwissRounds,
        CompletedTablesInRound = state.CompletedTablesInRound,
        TotalTablesInRound = 0,
        TargetMatches = state.TargetMatches,
        QueuedCustomTables = state.QueuedTables?.Count ?? 0,
        CompletedMatches = state.CompletedPairings?.Count ?? 0,
        MatchConcurrency = cfg?.Run?.MatchConcurrency ?? 1,
        LastError = state.LastError,
      };
    }

    private RunContext ResolveResumableOrNewLocked() {
      if (activeCtx != null && HasRemainingWork(activeCtx)) {
        return activeCtx;
      }
      var newestId = runManager.NewestRunId();
      var newest = newestId != null ? runManager.GetRun(newestId) : null;
      if (newest != null && HasRemainingWork(newest)) {
        return newest;
      }
      return runManager.CreateRun(currentConfig());
    }

    private static bool HasRemainingWork(RunContext ctx) {
      var st = ctx.RunState.Get();
      if (st == null) return true;
      if (st.QueuedTables is { Count: > 0 }) return true;
      if (st.Status == RunStatus.Finished) return false;
      string mode = (st.ScheduleMode ?? "swiss").ToLowerInvariant();
      return mode switch {
        "balanced" => st.CompletedPairings.Count < st.TargetMatches,
        "custom" => false,
        _ => st.SwissRoundIndex < st.SwissRounds,
      };
    }

    private RunState CreateInitialState(RunContext ctx, StartRunOptions options) {
      string mode = (options?.ScheduleMode ?? ctx.Config.Run.ScheduleMode ?? "swiss")
          .Trim().ToLowerInvariant();
      var queued = new List<QueuedBatchTable>();
      if (options?.CustomTables is { Count: > 0 }) {
        int idx = 1;
        foreach (var lineup in options.CustomTables) {
          queued.Add(new QueuedBatchTable {
            Label = $"Custom #{idx++}",
            SeatModelIds = new List<string>(lineup),
          });
        }
        if (string.IsNullOrWhiteSpace(options.ScheduleMode)) {
          mode = "custom";
        }
      }
      return new RunState {
        RunId = ctx.RunId,
        Label = options?.Label ?? ctx.Label ?? "",
        ScheduleMode = mode,
        SwissRoundIndex = 0,
        SwissRounds = options?.SwissRounds ?? ctx.Config.Run.SwissRounds,
        TargetMatches = options?.TargetMatches ?? ctx.Config.Run.TargetMatches,
        CompletedTablesInRound = 0,
        QueuedTables = queued,
        CompletedPairings = new List<CompletedPairing>(),
        Status = RunStatus.Running,
        StartedAt = clock().ToString("o"),
        UpdatedAt = clock().ToString("o"),
      };
    }

    private void BeginLoopLocked(RunContext ctx, StartRunOptions options) {
      activeCtx = ctx;
      activeRating = new RatingService(ctx.Config.Rating);
      activeSwiss = new SwissScheduler(ctx.Config.GameRules?.PlayerCount ?? 4);
      pauseRequested = false;
      cts = new CancellationTokenSource();
      var token = cts.Token;

      var existing = ctx.RunState.Get();
      if (existing != null && options == null &&
          (existing.QueuedTables.Count > 0 || existing.Status != RunStatus.Finished)) {
        liveState = existing;
        liveState.Status = RunStatus.Running;
        liveState.LastError = null;
      } else {
        liveState = CreateInitialState(ctx, options);
      }

      runId = liveState.RunId;
      status = RunStatus.Running;
      nextMatch = null;
      activeMatches.Clear();
      ctx.RunState.Save(liveState);

      loopTask = Task.Run(() => RunLoopAsync(ctx, token));
    }

    private void ReflectNewestRunForDisplay() {
      var newestId = runManager.NewestRunId();
      if (newestId == null) return;
      var ctx = runManager.GetRun(newestId);
      if (ctx == null) return;
      activeCtx = ctx;
      var persisted = ctx.RunState.Get();
      liveState = persisted;
      if (persisted != null) {
        status = persisted.Status is RunStatus.Running or RunStatus.Pausing
            ? RunStatus.Stopped
            : persisted.Status;
        runId = persisted.RunId;
      } else {
        runId = ctx.RunId;
        status = RunStatus.Idle;
      }
    }

    private async Task RunLoopAsync(RunContext ctx, CancellationToken token) {
      int concurrency = Math.Clamp(ctx.Config.Run?.MatchConcurrency ?? 1, 1, 16);
      try {
        while (!token.IsCancellationRequested && !pauseRequested) {
          // 1. Drain any queued custom/batch tables first (up to concurrency in parallel).
          if (HasQueuedTables()) {
            await DrainQueuedTablesAsync(ctx, concurrency, token);
            continue;
          }

          string mode;
          lock (gate) {
            mode = (liveState.ScheduleMode ?? "swiss").ToLowerInvariant();
          }

          if (mode == "balanced") {
            bool moreBalanced;
            lock (gate) {
              moreBalanced = liveState.CompletedPairings.Count < liveState.TargetMatches;
            }
            if (!moreBalanced) break;
            await RunBalancedWaveAsync(ctx, concurrency, token);
          } else if (mode == "swiss") {
            bool moreSwiss;
            lock (gate) {
              moreSwiss = liveState.SwissRoundIndex < liveState.SwissRounds;
            }
            if (!moreSwiss) break;
            await RunOneSwissRoundAsync(ctx, concurrency, token);
          } else {
            // "custom" mode finishes once QueuedTables is empty.
            break;
          }
        }

        lock (gate) {
          activeMatches.Clear();
          nextMatch = null;
          if (pauseRequested && HasRemainingWorkInState(liveState)) {
            status = RunStatus.Paused;
            liveState.Status = RunStatus.Paused;
            Logger.Log($"[Arena] Run {liveState.RunId} paused cleanly.");
          } else {
            status = RunStatus.Finished;
            liveState.Status = RunStatus.Finished;
            Logger.Log($"[Arena] Run {liveState.RunId} completed.");
          }
          liveState.UpdatedAt = clock().ToString("o");
          ctx.RunState.Save(liveState);
        }
      } catch (OperationCanceledException) {
        MarkHalted(ctx, RunStatus.Stopped, null);
        Logger.Log("[Arena] Run loop stopped.");
      } catch (Exception ex) {
        MarkHalted(ctx, RunStatus.Failed, ex.Message);
        Logger.Warn("[Arena] Run loop failed:");
        Logger.Warn(ex);
      }
    }

    private bool HasQueuedTables() {
      lock (gate) {
        return liveState.QueuedTables is { Count: > 0 };
      }
    }

    private static bool HasRemainingWorkInState(RunState st) {
      if (st == null) return false;
      if (st.QueuedTables is { Count: > 0 }) return true;
      string mode = (st.ScheduleMode ?? "swiss").ToLowerInvariant();
      return mode switch {
        "balanced" => st.CompletedPairings.Count < st.TargetMatches,
        "custom" => false,
        _ => st.SwissRoundIndex < st.SwissRounds,
      };
    }

    private async Task DrainQueuedTablesAsync(RunContext ctx, int concurrency, CancellationToken token) {
      var running = new List<Task>();
      using var sem = new SemaphoreSlim(concurrency, concurrency);

      while (!token.IsCancellationRequested && !pauseRequested) {
        await sem.WaitAsync(token);
        if (pauseRequested || token.IsCancellationRequested) {
          sem.Release();
          break;
        }

        SwissTable table = null;
        int roundTag = 0;
        lock (gate) {
          if (liveState.QueuedTables.Count == 0) {
            sem.Release();
            break;
          }
          var nextQ = liveState.QueuedTables[0];
          liveState.QueuedTables.RemoveAt(0);
          ctx.RunState.Save(liveState);

          table = MaterializeQueuedTableLocked(ctx, nextQ);
          roundTag = liveState.SwissRoundIndex;
          totalTablesInRound = liveState.QueuedTables.Count + 1;
        }

        if (table == null) {
          sem.Release();
          continue;
        }

        SetNextMatchPreview(table, roundTag + 1);
        running.Add(Task.Run(async () => {
          try {
            await ExecuteSingleTableAsync(ctx, table, roundTag, countTowardSwissRound: false, token);
          } finally {
            sem.Release();
          }
        }, token));
      }

      await Task.WhenAll(running);
    }

    private async Task RunBalancedWaveAsync(RunContext ctx, int concurrency, CancellationToken token) {
      var waveTables = new List<SwissTable>();
      int roundTag;
      lock (gate) {
        roundTag = liveState.SwissRoundIndex;
        int needed = Math.Min(concurrency, liveState.TargetMatches - liveState.CompletedPairings.Count);
        if (needed <= 0) return;

        var virtualStandings = BuildStandingsLocked(ctx).ToList();
        var virtualPairings = liveState.CompletedPairings
            .Select(p => (IReadOnlyCollection<string>)p.ModelIds)
            .ToList();
        var fillers = BaselineRoster(ctx);

        for (int k = 0; k < needed; k++) {
          var planned = activeSwiss.PlanNextBalancedTable(
              virtualStandings,
              virtualPairings,
              fillers,
              liveState.CompletedPairings.Count + k);
          if (planned == null) break;
          waveTables.Add(planned);
          virtualPairings.Add(planned.ModelIds);
          var usedIds = planned.Assignments.Select(a => a.Model.Id).ToList();
          virtualStandings = virtualStandings.Select(s => new SwissStanding {
            Model = s.Model,
            Elo = s.Elo,
            GamesPlayed = s.GamesPlayed + usedIds.Count(id => id == s.ModelId),
          }).ToList();
        }
        totalTablesInRound = liveState.TargetMatches;
      }

      if (waveTables.Count == 0) {
        throw new InvalidOperationException("No enabled models available to schedule balanced matches.");
      }

      SetNextMatchPreview(waveTables[0], 1);
      var tasks = waveTables.Select(t =>
          ExecuteSingleTableAsync(ctx, t, roundTag, countTowardSwissRound: false, token)).ToList();
      await Task.WhenAll(tasks);
    }

    private async Task RunOneSwissRoundAsync(RunContext ctx, int concurrency, CancellationToken token) {
      IReadOnlyList<SwissTable> tables;
      int startIndex;
      int roundIndex;
      lock (gate) {
        roundIndex = liveState.SwissRoundIndex;
        var standings = BuildStandingsLocked(ctx);
        if (standings.Count == 0) {
          throw new InvalidOperationException("No enabled models in roster to pair a Swiss round.");
        }
        var prior = liveState.CompletedPairings
            .Select(p => (IReadOnlyCollection<string>)p.ModelIds)
            .ToList();
        var fillers = BaselineRoster(ctx);
        tables = activeSwiss.Pair(standings, prior, fillers.Count > 0 ? fillers : standings.Select(s => s.Model).ToList());
        startIndex = Math.Clamp(liveState.CompletedTablesInRound, 0, tables.Count);
        totalTablesInRound = tables.Count;
      }

      using var sem = new SemaphoreSlim(concurrency, concurrency);
      var tasks = new List<Task>();

      for (int t = startIndex; t < tables.Count; t++) {
        if (pauseRequested || token.IsCancellationRequested) break;
        await sem.WaitAsync(token);
        if (pauseRequested || token.IsCancellationRequested) {
          sem.Release();
          break;
        }

        var table = tables[t];
        SetNextMatchPreview(table, roundIndex + 1);
        tasks.Add(Task.Run(async () => {
          try {
            await ExecuteSingleTableAsync(ctx, table, roundIndex, countTowardSwissRound: true, token);
          } finally {
            sem.Release();
          }
        }, token));
      }

      await Task.WhenAll(tasks);

      lock (gate) {
        if (!pauseRequested && !token.IsCancellationRequested &&
            liveState.CompletedTablesInRound >= tables.Count) {
          liveState.SwissRoundIndex++;
          liveState.CompletedTablesInRound = 0;
          liveState.UpdatedAt = clock().ToString("o");
          ctx.RunState.Save(liveState);
        }
      }
    }

    private SwissTable MaterializeQueuedTableLocked(RunContext ctx, QueuedBatchTable q) {
      if (q.SeatModelIds == null || q.SeatModelIds.Count == 0) {
        var standings = BuildStandingsLocked(ctx);
        var prior = liveState.CompletedPairings
            .Select(p => (IReadOnlyCollection<string>)p.ModelIds)
            .ToList();
        var fillers = BaselineRoster(ctx);
        return activeSwiss.PlanNextBalancedTable(
            standings,
            prior,
            fillers.Count > 0 ? fillers : standings.Select(s => s.Model).ToList(),
            liveState.CompletedPairings.Count);
      }

      var byId = ctx.Config.Models
          .GroupBy(m => m.Id, StringComparer.Ordinal)
          .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
      var assignments = new List<EvalSeatAssignment>(q.SeatModelIds.Count);
      for (int i = 0; i < q.SeatModelIds.Count; i++) {
        if (!byId.TryGetValue(q.SeatModelIds[i], out var model)) {
          return null;
        }
        assignments.Add(new EvalSeatAssignment { Seat = i, Model = model });
      }
      return new SwissTable {
        Label = q.Label ?? "Custom",
        Assignments = assignments,
        Padded = false,
      };
    }

    private async Task ExecuteSingleTableAsync(
        RunContext ctx,
        SwissTable table,
        int swissRoundIndex,
        bool countTowardSwissRound,
        CancellationToken token) {
      string gameId = NewGameId();
      var runner = tableRunner ?? BuildEvalRoomRunner(ctx);

      lock (gate) {
        activeMatches[gameId] = new LiveMatchProgress {
          GameId = gameId,
          RoundLabel = "Starting...",
          WallRemaining = 0,
          TurnCount = 0,
          StartedAt = clock().ToString("o"),
          Seats = table.Assignments.Select(a => new LiveSeatProgress {
            Seat = a.Seat,
            ModelId = a.Model.Id,
            DisplayName = a.Model.DisplayName,
            Points = ctx.Config.GameRules?.InitialPoints ?? 25000,
          }).ToList(),
        };
      }

      try {
        var result = await runner(
            table,
            gameId,
            token,
            progress => {
              lock (gate) {
                activeMatches[gameId] = progress;
              }
            });
        token.ThrowIfCancellationRequested();

        lock (gate) {
          var participants = BuildRatingParticipants(ctx, table, result);
          var changes = activeRating.ApplyMatch(ctx.Ratings, participants);
          var roomHelper = new EvalRoom(ctx.Config, replayStore: null);
          var record = roomHelper.BuildMatchRecord(
              result,
              changes,
              matchId: gameId,
              runId: liveState.RunId,
              swissRound: swissRoundIndex,
              batchLabel: table.Label);
          ctx.Matches.Append(record);

          liveState.CompletedPairings.Add(new CompletedPairing {
            SwissRound = swissRoundIndex,
            MatchId = record.MatchId,
            ModelIds = table.ModelIds.ToList(),
          });
          if (countTowardSwissRound) {
            liveState.CompletedTablesInRound++;
          }
          liveState.UpdatedAt = clock().ToString("o");
          ctx.RunState.Save(liveState);
          nextMatch = null;
        }
        Logger.Log($"[Arena] Match {gameId} completed ({result.RoundsPlayed} rounds, {result.DurationMs} ms).");
      } finally {
        lock (gate) {
          activeMatches.Remove(gameId);
        }
      }
    }

    private TableRunner BuildEvalRoomRunner(RunContext ctx) {
      if (evalRoomFactory == null) {
        throw new InvalidOperationException(
            "ArenaService requires either a tableRunner or evalRoomFactory.");
      }
      return (table, gameId, token, onProgress) =>
          evalRoomFactory(ctx.Config).RunAsync(table.Assignments, gameId, token, onProgress);
    }

    private static IReadOnlyList<RatingParticipant> BuildRatingParticipants(
        RunContext ctx, SwissTable table, EvalResult result) {
      var modelById = table.Assignments
          .Select(a => a.Model)
          .GroupBy(m => m.Id, StringComparer.Ordinal)
          .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
      return result.Seats.Select(s => {
        modelById.TryGetValue(s.ModelId, out var model);
        double? frozen = model != null && model.IsBaseline ? model.FrozenElo : null;
        double current = ctx.Ratings.Get(s.ModelId)?.Elo
            ?? frozen ?? ctx.Config.Rating.InitialElo;
        return new RatingParticipant {
          ModelId = s.ModelId,
          Elo = current,
          Placement = s.Placement,
          FrozenElo = frozen,
          PenaltyCount = s.PenaltyCount,
        };
      }).ToList();
    }

    private IReadOnlyList<SwissStanding> BuildStandingsLocked(RunContext ctx) {
      return EnabledRoster(ctx).Select(m => {
        var r = ctx.Ratings.Get(m.Id);
        return new SwissStanding {
          Model = m,
          Elo = r?.Elo ?? m.FrozenElo ?? ctx.Config.Rating.InitialElo,
          GamesPlayed = r?.Games ?? 0,
        };
      }).ToList();
    }

    private static IReadOnlyList<ArenaConfig.ModelConfig> EnabledRoster(RunContext ctx) =>
        ctx.Config.Models.Where(m => m.Enabled).ToList();

    private static IReadOnlyList<ArenaConfig.ModelConfig> BaselineRoster(RunContext ctx) =>
        EnabledRoster(ctx).Where(m => m.IsBaseline).ToList();

    private void SetNextMatchPreview(SwissTable table, int swissRound) {
      lock (gate) {
        nextMatch = new NextMatchPreview {
          SwissRound = swissRound,
          Label = table.Label ?? "",
          Padded = table.Padded,
          Seats = table.Assignments.Select(a => new NextMatchSeat {
            Seat = a.Seat,
            ModelId = a.Model.Id,
            DisplayName = a.Model.DisplayName,
          }).ToList(),
        };
      }
    }

    private void MarkHalted(RunContext ctx, RunStatus targetStatus, string error) {
      lock (gate) {
        activeMatches.Clear();
        nextMatch = null;
        if (status != RunStatus.Finished && liveState != null) {
          status = targetStatus;
          liveState.Status = targetStatus;
          liveState.LastError = error;
          liveState.UpdatedAt = clock().ToString("o");
          ctx.RunState.Save(liveState);
        }
      }
    }

    private static string NewGameId() =>
        $"{DateTime.UtcNow:yyyyMMdd'T'HHmmss}-{Guid.NewGuid():N}"[..40];
  }
}
