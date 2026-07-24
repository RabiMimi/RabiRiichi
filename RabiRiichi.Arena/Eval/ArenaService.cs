using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Utils;

namespace RabiRiichi.Arena.Eval {
  /// <summary>
  /// A live, read-only status snapshot of the run scheduler, safe to hand to
  /// controllers / the public page (§10/§12a). Never contains secrets.
  /// </summary>
  public sealed class ArenaStatus {
    public RunStatus Status { get; init; }
    public string RunId { get; init; } = "";

    /// <summary>1-based Swiss round currently in progress (0 when idle).</summary>
    public int CurrentSwissRound { get; init; }

    /// <summary>Total Swiss rounds this run targets.</summary>
    public int TotalSwissRounds { get; init; }

    /// <summary>Tables of the current round already finished.</summary>
    public int CompletedTablesInRound { get; init; }

    /// <summary>Total tables in the current round (0 when idle).</summary>
    public int TotalTablesInRound { get; init; }

    /// <summary>Total matches finished across the whole run.</summary>
    public int CompletedMatches { get; init; }

    /// <summary>The upcoming pairing preview, or null if none is pending.</summary>
    public NextMatchPreview NextMatch { get; init; }

    /// <summary>
    /// Seconds until the next match starts when in cooldown; null otherwise.
    /// </summary>
    public double? SecondsToNextMatch { get; init; }
  }

  /// <summary>
  /// The upcoming pairing for the public "next match" panel (§12a). Contains
  /// display names + model ids for the seats and whether the table was padded
  /// with baseline fillers. Never includes the seed (in-progress preview).
  /// </summary>
  public sealed class NextMatchPreview {
    public int SwissRound { get; init; }
    public bool Padded { get; init; }
    public IReadOnlyList<NextMatchSeat> Seats { get; init; } =
        Array.Empty<NextMatchSeat>();
  }

  public sealed class NextMatchSeat {
    public int Seat { get; init; }
    public string ModelId { get; init; } = "";
    public string DisplayName { get; init; } = "";
  }

  /// <summary>
  /// The run scheduler (ARENA_DESIGN.md §10, multi-run extension). A <b>run</b> is
  /// a Swiss tournament over the ENABLED roster (LLMs + baselines) of the config
  /// <em>snapshot frozen when the run was created</em>. Each run is fully isolated
  /// on disk (own config snapshot, ratings, matches, run.json) via
  /// <see cref="RunManager"/>, so editing the live config or starting a new run
  /// never disturbs an existing run's results.
  ///
  /// This class owns the scheduling loop, start/resume + new-run control,
  /// cooldown, the next-match preview, and the resumable per-run state. Only ONE
  /// run loop is active at a time — the "active run".
  ///
  /// It is hosting-agnostic: the "run one table" function, the clock/delay, and
  /// the config-to-snapshot source are all injectable so tests run instantly
  /// without ASP.NET, real sleeps, or full games.
  /// </summary>
  public sealed class ArenaService {
    /// <summary>
    /// Runs a single table to completion and returns its result. Injected so
    /// scheduler tests supply a fast fake instead of a real game; production
    /// wires an <see cref="EvalRoom"/> built from the active run's config.
    /// </summary>
    public delegate Task<EvalResult> TableRunner(
        SwissTable table, string gameId, CancellationToken cancellationToken);

    /// <summary>
    /// Awaitable delay used for cooldowns. Injected so tests pass a zero/virtual
    /// delay and assert its invocation count without real sleeps.
    /// </summary>
    public delegate Task DelayFn(TimeSpan delay, CancellationToken cancellationToken);

    private readonly RunManager runManager;
    private readonly Func<ArenaConfig> currentConfig;
    private readonly TableRunner tableRunner;
    private readonly Func<ArenaConfig, EvalRoom> evalRoomFactory;
    private readonly DelayFn delayFn;
    private readonly Func<DateTime> clock;

    private readonly object gate = new();
    private CancellationTokenSource cts;
    private Task loopTask;

    // The active run's context + per-run collaborators (set when a loop begins).
    private RunContext activeCtx;
    private RatingService activeRating;
    private SwissScheduler activeSwiss;

    // Live status fields (guarded by gate).
    private RunStatus status = RunStatus.Idle;
    private string runId = "";
    private int swissRoundIndex;
    private int swissRoundsTarget;
    private int completedTablesInRound;
    private int totalTablesInRound;
    private int completedMatches;
    private NextMatchPreview nextMatch;
    private DateTime? nextMatchStartUtc;

    /// <param name="runManager">Owns the per-run directories/stores + run list.</param>
    /// <param name="currentConfig">
    /// Supplies the config to FREEZE when a new run is created (the live editable
    /// config in production; a fixed config in tests). Cloned by the run manager.
    /// </param>
    /// <param name="tableRunner">
    /// Runs one table; when null, a runner backed by a fresh <see cref="EvalRoom"/>
    /// per match (built from the active run's snapshot config via
    /// <paramref name="evalRoomFactory"/>) is used.
    /// </param>
    /// <param name="evalRoomFactory">
    /// Builds the <see cref="EvalRoom"/> for the active run from its snapshot
    /// config; ignored if <paramref name="tableRunner"/> is supplied.
    /// </param>
    /// <param name="delayFn">Cooldown delay; defaults to <c>Task.Delay</c>.</param>
    /// <param name="clock">UTC clock; defaults to <c>DateTime.UtcNow</c>.</param>
    public ArenaService(
        RunManager runManager,
        Func<ArenaConfig> currentConfig,
        TableRunner tableRunner = null,
        Func<ArenaConfig, EvalRoom> evalRoomFactory = null,
        DelayFn delayFn = null,
        Func<DateTime> clock = null) {
      this.runManager = runManager ?? throw new ArgumentNullException(nameof(runManager));
      this.currentConfig = currentConfig
          ?? throw new ArgumentNullException(nameof(currentConfig));
      this.tableRunner = tableRunner;
      this.evalRoomFactory = evalRoomFactory;
      this.delayFn = delayFn ?? Task.Delay;
      this.clock = clock ?? (() => DateTime.UtcNow);

      // Reflect the newest run's persisted state into the initial status so the
      // public page shows the current run immediately after a restart (a crash
      // mid-run reflects as Stopped until an admin Start resumes it).
      ReflectNewestRunForDisplay();
    }

    /// <summary>The id of the current/active run (newest), or "" if none exists.</summary>
    public string ActiveRunId {
      get { lock (gate) { return runId; } }
    }

    // ----- Public control surface -----------------------------------------

    /// <summary>
    /// Starts or RESUMES the current run: resumes the newest run if it still has
    /// unfinished rounds, otherwise creates a fresh run (snapshotting the current
    /// config). Idempotent while a loop is already active (no-op). Returns
    /// immediately; scheduling proceeds on a background task.
    /// </summary>
    public void Start() {
      lock (gate) {
        if (loopTask != null && !loopTask.IsCompleted) {
          Logger.Log("[Arena] Start ignored: a run loop is already active.");
          return;
        }
        var ctx = ResolveResumableOrNewLocked();
        BeginLoopLocked(ctx);
        Logger.Log($"[Arena] Run loop started for {ctx.RunId}.");
      }
    }

    /// <summary>
    /// Starts a brand-NEW run: stops any active loop, freezes the current config
    /// into a new run, and begins it. This is the explicit admin "New run" action;
    /// past runs keep their own snapshot/ratings/matches untouched.
    /// </summary>
    public async Task StartNewRunAsync() {
      await StopAsync();
      lock (gate) {
        var ctx = runManager.CreateRun(currentConfig());
        BeginLoopLocked(ctx);
        Logger.Log($"[Arena] New run {ctx.RunId} started.");
      }
    }

    /// <summary>
    /// Halts scheduling and cancels the in-flight match. Non-blocking; use
    /// <see cref="StopAsync"/> to await the loop's teardown.
    /// </summary>
    public void Stop() {
      lock (gate) {
        if (cts == null) {
          return;
        }
        Logger.Log("[Arena] Stop requested; cancelling in-flight match.");
        try {
          cts.Cancel();
        } catch (ObjectDisposedException) {
          // Already torn down; nothing to cancel.
        }
      }
    }

    /// <summary>
    /// Halts scheduling, cancels the in-flight match, and awaits the loop's exit.
    /// Safe to call repeatedly.
    /// </summary>
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
          // Expected on Stop.
        }
      }
    }

    /// <summary>Returns a consistent snapshot of the active run's status (§12a).</summary>
    public ArenaStatus GetStatus() {
      lock (gate) {
        double? toNext = null;
        if (status == RunStatus.Cooldown && nextMatchStartUtc.HasValue) {
          toNext = Math.Max(0.0, (nextMatchStartUtc.Value - clock()).TotalSeconds);
        }
        return new ArenaStatus {
          Status = status,
          RunId = runId,
          CurrentSwissRound = status == RunStatus.Idle ? 0 : swissRoundIndex + 1,
          TotalSwissRounds = swissRoundsTarget,
          CompletedTablesInRound = completedTablesInRound,
          TotalTablesInRound = totalTablesInRound,
          CompletedMatches = completedMatches,
          NextMatch = nextMatch,
          SecondsToNextMatch = toNext,
        };
      }
    }

    /// <summary>The upcoming pairing for the public page, or null if none pending (§12a).</summary>
    public NextMatchPreview GetNextMatchPreview() {
      lock (gate) {
        return nextMatch;
      }
    }

    /// <summary>
    /// Builds a read-only status for a NON-active run purely from its persisted
    /// state — used by the public page to render older/finished runs (no live
    /// preview or cooldown countdown). Never touches the scheduler.
    /// </summary>
    public static ArenaStatus BuildStatusFromState(RunState state, ArenaConfig cfg) {
      if (state == null) {
        return new ArenaStatus {
          Status = RunStatus.Idle,
          TotalSwissRounds = cfg?.Run.SwissRounds ?? 0,
        };
      }
      return new ArenaStatus {
        Status = state.Status,
        RunId = state.RunId,
        CurrentSwissRound =
            state.Status == RunStatus.Idle ? 0 : state.SwissRoundIndex + 1,
        TotalSwissRounds = state.SwissRounds,
        CompletedTablesInRound = state.CompletedTablesInRound,
        TotalTablesInRound = 0,
        CompletedMatches = state.CompletedPairings.Count,
        NextMatch = null,
        SecondsToNextMatch = null,
      };
    }

    // ----- Run selection + loop bootstrap ---------------------------------

    private RunContext ResolveResumableOrNewLocked() {
      var newestId = runManager.NewestRunId();
      var ctx = newestId != null ? runManager.GetRun(newestId) : null;
      if (ctx != null) {
        var state = ctx.RunState.Get();
        bool resumable = state == null
            || (state.Status != RunStatus.Finished
                && state.SwissRoundIndex < state.SwissRounds);
        if (resumable) {
          return ctx;
        }
      }
      return runManager.CreateRun(currentConfig());
    }

    private void BeginLoopLocked(RunContext ctx) {
      activeCtx = ctx;
      activeRating = new RatingService(ctx.Config.Rating);
      activeSwiss = new SwissScheduler(ctx.Config.Run.PlayerCount);
      cts = new CancellationTokenSource();
      var token = cts.Token;
      var resume = ctx.RunState.Get();

      // Reflect the active run in the live status synchronously, so a control
      // response (e.g. admin Start) observes Running immediately rather than the
      // pre-start snapshot until the loop thread initializes state.
      runId = resume?.RunId ?? ctx.RunId;
      status = RunStatus.Running;
      swissRoundsTarget = resume?.SwissRounds ?? ctx.Config.Run.SwissRounds;
      swissRoundIndex = resume?.SwissRoundIndex ?? 0;
      completedTablesInRound = resume?.CompletedTablesInRound ?? 0;
      completedMatches = resume?.CompletedPairings.Count ?? 0;
      nextMatch = null;
      nextMatchStartUtc = null;

      loopTask = Task.Run(() => RunLoopAsync(ctx, resume, token));
    }

    private void ReflectNewestRunForDisplay() {
      var newestId = runManager.NewestRunId();
      if (newestId == null) {
        return;
      }
      var ctx = runManager.GetRun(newestId);
      if (ctx == null) {
        return;
      }
      activeCtx = ctx;
      var persisted = ctx.RunState.Get();
      if (persisted != null) {
        status = persisted.Status is RunStatus.Running or RunStatus.Cooldown
            ? RunStatus.Stopped // a crash mid-run resumes as Stopped until Start.
            : persisted.Status;
        runId = persisted.RunId;
        swissRoundIndex = persisted.SwissRoundIndex;
        swissRoundsTarget = persisted.SwissRounds;
        completedTablesInRound = persisted.CompletedTablesInRound;
        completedMatches = persisted.CompletedPairings.Count;
      } else {
        runId = ctx.RunId;
        status = RunStatus.Idle;
        swissRoundsTarget = ctx.Config.Run.SwissRounds;
      }
    }

    // ----- The scheduling loop --------------------------------------------

    private async Task RunLoopAsync(
        RunContext ctx, RunState resume, CancellationToken token) {
      RunState state = InitOrResumeState(ctx, resume);
      try {
        for (; state.SwissRoundIndex < state.SwissRounds; state.SwissRoundIndex++) {
          token.ThrowIfCancellationRequested();

          var tables = PlanRound(ctx, state);
          SetRoundStatus(state, tables.Count);

          // Skip tables of this round that a prior (resumed) run already finished.
          for (int t = state.CompletedTablesInRound; t < tables.Count; t++) {
            token.ThrowIfCancellationRequested();

            // Publish the upcoming pairing BEFORE the cooldown so the public page
            // shows "next match" during the countdown (not just once play starts).
            SetNextMatchPreview(tables[t]);

            // Cooldown BEFORE a match (except the very first of the whole run).
            if (completedMatches > 0) {
              await CooldownAsync(ctx, token);
            }

            SetStatus(RunStatus.Running);

            var (record, pairing) = await PlayTableAsync(ctx, tables[t], state, token);
            RecordCompletion(ctx, state, pairing, record);
          }

          state.CompletedTablesInRound = 0;
        }

        FinishRun(ctx, state);
      } catch (OperationCanceledException) {
        MarkStopped(ctx, state);
        Logger.Log("[Arena] Run loop stopped.");
      } catch (Exception e) {
        MarkStopped(ctx, state);
        Logger.Warn("[Arena] Run loop aborted by error:");
        Logger.Warn(e);
      }
    }

    private RunState InitOrResumeState(RunContext ctx, RunState resume) {
      lock (gate) {
        RunState state;
        bool resumable = resume != null
            && resume.Status != RunStatus.Finished
            && resume.SwissRoundIndex < resume.SwissRounds;
        if (resumable) {
          state = resume;
          Logger.Log(
              $"[Arena] Resuming run {state.RunId} at round " +
              $"{state.SwissRoundIndex + 1}/{state.SwissRounds}.");
        } else {
          state = new RunState {
            RunId = ctx.RunId,
            SwissRoundIndex = 0,
            SwissRounds = ctx.Config.Run.SwissRounds,
            CompletedTablesInRound = 0,
            CompletedPairings = new List<CompletedPairing>(),
            Status = RunStatus.Running,
            StartedAt = clock().ToString("o"),
          };
          Logger.Log($"[Arena] Starting new run {state.RunId}.");
        }
        runId = state.RunId;
        swissRoundIndex = state.SwissRoundIndex;
        swissRoundsTarget = state.SwissRounds;
        completedTablesInRound = state.CompletedTablesInRound;
        completedMatches = state.CompletedPairings.Count;
        status = RunStatus.Running;
        PersistLocked(ctx, state);
        return state;
      }
    }

    private IReadOnlyList<SwissTable> PlanRound(RunContext ctx, RunState state) {
      var standings = BuildStandings(ctx);
      var priorPairings = state.CompletedPairings
          .Select(p => (IReadOnlyCollection<string>)p.ModelIds)
          .ToList();
      var fillers = BaselineRoster(ctx);
      return activeSwiss.Pair(standings, priorPairings, fillers);
    }

    private async Task<(MatchRecord record, CompletedPairing pairing)> PlayTableAsync(
        RunContext ctx, SwissTable table, RunState state, CancellationToken token) {
      string gameId = NewGameId();
      var runner = tableRunner ?? BuildEvalRoomRunner(ctx);
      var result = await runner(table, gameId, token);
      token.ThrowIfCancellationRequested();

      // Apply rating + append the match record via the existing (non-duplicated)
      // rating math and per-run stores.
      var participants = BuildRatingParticipants(ctx, table, result);
      var changes = activeRating.ApplyMatch(ctx.Ratings, participants);
      var record = BuildMatchRecord(ctx, result, changes, gameId, state, table);
      ctx.Matches.Append(record);

      var pairing = new CompletedPairing {
        SwissRound = state.SwissRoundIndex,
        MatchId = record.MatchId,
        ModelIds = table.ModelIds.ToList(),
      };
      return (record, pairing);
    }

    private TableRunner BuildEvalRoomRunner(RunContext ctx) {
      if (evalRoomFactory == null) {
        throw new InvalidOperationException(
            "Cannot play a match: ArenaService was constructed without a " +
            "tableRunner or evalRoomFactory. Wire an evalRoomFactory in DI.");
      }
      return (table, gameId, token) =>
          evalRoomFactory(ctx.Config).RunAsync(table.Assignments, gameId, token);
    }

    // ----- Rating + record wiring (reuses EvalRoom/RatingService, no dup math) -

    private IReadOnlyList<RatingParticipant> BuildRatingParticipants(
        RunContext ctx, SwissTable table, EvalResult result) {
      var modelBySeat = table.Assignments.ToDictionary(a => a.Seat, a => a.Model);
      return result.Seats.Select(s => {
        modelBySeat.TryGetValue(s.Seat, out var model);
        double? frozen = model != null && IsBaseline(model) ? model.FrozenElo : null;
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

    private MatchRecord BuildMatchRecord(
        RunContext ctx, EvalResult result,
        IReadOnlyDictionary<string, EloChange> eloChanges,
        string matchId, RunState state, SwissTable table) {
      var players = result.Seats.Select(s => {
        eloChanges.TryGetValue(s.ModelId, out var change);
        return new MatchPlayer {
          Seat = s.Seat,
          ModelId = s.ModelId,
          DisplayName = s.DisplayName,
          FinalPoints = (int)s.FinalPoints,
          Placement = s.Placement,
          PenaltyCount = s.PenaltyCount,
          EloBefore = change?.EloBefore ?? 0,
          EloAfter = change?.EloAfter ?? 0,
        };
      }).ToList();

      return new MatchRecord {
        MatchId = matchId,
        GameId = result.GameId,
        RunId = state.RunId,
        SwissRound = state.SwissRoundIndex,
        StartedAt = result.StartedAt,
        FinishedAt = result.FinishedAt,
        Seed = unchecked((long)result.Seed),
        Config = result.Config,
        Players = players,
      };
    }

    // ----- Standings + roster helpers -------------------------------------

    private IReadOnlyList<SwissStanding> BuildStandings(RunContext ctx) {
      return EnabledRoster(ctx).Select(m => new SwissStanding {
        Model = m,
        Elo = ctx.Ratings.Get(m.Id)?.Elo
            ?? m.FrozenElo ?? ctx.Config.Rating.InitialElo,
      }).ToList();
    }

    private static IReadOnlyList<ArenaConfig.ModelConfig> EnabledRoster(RunContext ctx) =>
        ctx.Config.Models.Where(m => m.Enabled).ToList();

    private static IReadOnlyList<ArenaConfig.ModelConfig> BaselineRoster(RunContext ctx) =>
        EnabledRoster(ctx).Where(IsBaseline).ToList();

    private static bool IsBaseline(ArenaConfig.ModelConfig m) =>
        string.Equals(m.Provider, "baseline", StringComparison.OrdinalIgnoreCase);

    // ----- Status transitions (all under gate) ----------------------------

    private void SetRoundStatus(RunState state, int totalTables) {
      lock (gate) {
        swissRoundIndex = state.SwissRoundIndex;
        totalTablesInRound = totalTables;
        completedTablesInRound = state.CompletedTablesInRound;
      }
    }

    private void SetStatus(RunStatus s) {
      lock (gate) {
        status = s;
      }
    }

    private void SetNextMatchPreview(SwissTable table) {
      lock (gate) {
        nextMatch = new NextMatchPreview {
          SwissRound = swissRoundIndex + 1,
          Padded = table.Padded,
          Seats = table.Assignments.Select(a => new NextMatchSeat {
            Seat = a.Seat,
            ModelId = a.Model.Id,
            DisplayName = a.Model.DisplayName,
          }).ToList(),
        };
      }
    }

    private async Task CooldownAsync(RunContext ctx, CancellationToken token) {
      int seconds = ctx.Config.Run.CooldownSecondsBetweenMatches;
      if (seconds <= 0) {
        // Still invoke the delay seam once (tests assert its invocation count).
        await delayFn(TimeSpan.Zero, token);
        return;
      }
      lock (gate) {
        status = RunStatus.Cooldown;
        nextMatchStartUtc = clock().AddSeconds(seconds);
      }
      await delayFn(TimeSpan.FromSeconds(seconds), token);
      lock (gate) {
        nextMatchStartUtc = null;
      }
    }

    private void RecordCompletion(
        RunContext ctx, RunState state, CompletedPairing pairing, MatchRecord record) {
      lock (gate) {
        state.CompletedPairings.Add(pairing);
        state.CompletedTablesInRound++;
        completedTablesInRound = state.CompletedTablesInRound;
        completedMatches = state.CompletedPairings.Count;
        nextMatch = null;
        PersistLocked(ctx, state);
      }
      Logger.Log(
          $"[Arena] Match {record.MatchId} done (round {state.SwissRoundIndex + 1}, " +
          $"table {state.CompletedTablesInRound}/{totalTablesInRound}).");
    }

    private void FinishRun(RunContext ctx, RunState state) {
      lock (gate) {
        state.Status = RunStatus.Finished;
        status = RunStatus.Finished;
        nextMatch = null;
        nextMatchStartUtc = null;
        PersistLocked(ctx, state);
      }
      Logger.Log($"[Arena] Run {state.RunId} finished.");
    }

    private void MarkStopped(RunContext ctx, RunState state) {
      lock (gate) {
        if (status != RunStatus.Finished) {
          status = RunStatus.Stopped;
          state.Status = RunStatus.Stopped;
          nextMatchStartUtc = null;
          PersistLocked(ctx, state);
        }
      }
    }

    private void PersistLocked(RunContext ctx, RunState state) {
      state.SwissRoundIndex = swissRoundIndex;
      state.CompletedTablesInRound = completedTablesInRound;
      state.UpdatedAt = clock().ToString("o");
      ctx.RunState.Save(state);
    }

    private static string NewGameId() =>
        $"{DateTime.UtcNow:yyyyMMdd'T'HHmmss}-{Guid.NewGuid():N}".Substring(0, 40);
  }
}
