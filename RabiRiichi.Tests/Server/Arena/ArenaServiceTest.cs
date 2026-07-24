using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Arena.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Server.Arena {
  /// <summary>
  /// Scheduler tests for <see cref="ArenaService"/> (ARENA_DESIGN.md §10). These
  /// inject a FAKE table runner (no full games) + a zero/virtual cooldown delay,
  /// so the loop runs instantly and deterministically. One separate integration
  /// test (in <c>EvalRoomTest</c>) exercises the real EvalRoom wiring.
  /// </summary>
  [TestClass]
  public class ArenaServiceTest {
    private string workspaceDir;
    private RunManager runManager;

    [TestInitialize]
    public void Setup() {
      workspaceDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_svc_{Guid.NewGuid():N}");
      Directory.CreateDirectory(workspaceDir);
    }

    [TestCleanup]
    public void Cleanup() {
      if (Directory.Exists(workspaceDir)) {
        try { Directory.Delete(workspaceDir, recursive: true); } catch { }
      }
    }

    // ----- Config / roster helpers ----------------------------------------

    private static ArenaConfig.ModelConfig Llm(string id) => new() {
      Id = id,
      DisplayName = id.ToUpperInvariant(),
      Provider = "openai",
      Model = "m",
      Enabled = true,
    };

    private static ArenaConfig.ModelConfig Baseline(string id, double frozen = 1500) => new() {
      Id = id,
      DisplayName = id.ToUpperInvariant(),
      Provider = "baseline",
      Variant = "default",
      FrozenElo = frozen,
      Enabled = true,
    };

    private ArenaConfig Config(
        int swissRounds, int cooldown, params ArenaConfig.ModelConfig[] models) {
      var cfg = new ArenaConfig {
        WorkspaceDir = workspaceDir,
        AdminPassword = "pw",
        ClientUrl = "https://play.example.com",
        WsUrl = "wss://arena.example.com",
      };
      cfg.Run.SwissRounds = swissRounds;
      cfg.Run.PlayerCount = 4;
      cfg.Run.TotalRound = 1;
      cfg.Run.CooldownSecondsBetweenMatches = cooldown;
      cfg.Models.AddRange(models);
      return cfg;
    }

    // 8 enabled LLMs => divisible by 4 => exactly 2 tables/round, no filler
    // needed. (Baseline filling is covered by SwissSchedulerTest directly.)
    private static ArenaConfig.ModelConfig[] EightLlms() =>
        Enumerable.Range(0, 8).Select(i => Llm($"p{i}")).ToArray();

    private static ArenaConfig.ModelConfig[] FourBaselines() =>
        new[] { Baseline("a"), Baseline("b"), Baseline("c"), Baseline("d") };

    // ----- A deterministic fake table runner ------------------------------

    /// <summary>
    /// Produces an <see cref="EvalResult"/> for a table without playing a game:
    /// placement 1..4 by seat order, fixed points. Records every gameId run.
    /// </summary>
    private sealed class FakeRunner {
      public readonly ConcurrentQueue<string> Ran = new();
      public int Count => Ran.Count;
      public volatile bool Observed; // set true when a run starts

      private readonly TaskCompletionSource block;

      public FakeRunner(TaskCompletionSource block = null) {
        this.block = block;
      }

      public async Task<EvalResult> Run(
          SwissTable table, string gameId, CancellationToken token) {
        Observed = true;
        Ran.Enqueue(gameId);
        if (block != null) {
          // Hold the match "in flight" until released / cancelled (Stop test).
          using var reg = token.Register(() => block.TrySetCanceled(token));
          await block.Task;
        }
        token.ThrowIfCancellationRequested();
        var seats = table.Assignments.Select((a, i) => new EvalSeatResult {
          Seat = a.Seat,
          ModelId = a.Model.Id,
          DisplayName = a.Model.DisplayName,
          FinalPoints = 25000 + (4 - i) * 1000,
          Placement = i + 1,
          PenaltyCount = 0,
        }).ToList();
        return new EvalResult {
          GameId = gameId,
          Seed = 12345,
          Seats = seats,
          Config = null,
          StartedAt = DateTime.UtcNow.ToString("o"),
          FinishedAt = DateTime.UtcNow.ToString("o"),
          Completed = true,
        };
      }
    }

    private ArenaService Build(
        ArenaConfig cfg, ArenaService.TableRunner runner,
        ArenaService.DelayFn delay = null) {
      runManager = new RunManager(workspaceDir);
      return new ArenaService(
          runManager,
          currentConfig: () => cfg,
          tableRunner: runner,
          delayFn: delay);
    }

    /// <summary>The active (newest) run's stores, for post-run assertions.</summary>
    private RunContext ActiveRun() => runManager.GetRun(runManager.NewestRunId());

    private static async Task WaitUntil(Func<bool> cond, int timeoutMs = 5000) {
      var sw = Stopwatch.StartNew();
      while (!cond()) {
        if (sw.ElapsedMilliseconds > timeoutMs) {
          Assert.Fail("Condition not met within timeout.");
        }
        await Task.Delay(10);
      }
    }

    // ----- Start runs the scheduled matches -------------------------------

    [TestMethod]
    public async Task Start_RunsAllTablesOfAllRounds() {
      // 8 LLMs => 2 tables per round; 3 rounds => 6 matches.
      var cfg = Config(swissRounds: 3, cooldown: 0, EightLlms());
      var fake = new FakeRunner();
      var svc = Build(cfg, fake.Run);

      svc.Start();
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);

      Assert.AreEqual(6, fake.Count, "3 rounds * 2 tables");
      var status = svc.GetStatus();
      Assert.AreEqual(RunStatus.Finished, status.Status);
      Assert.AreEqual(6, status.CompletedMatches);

      // Match records + ratings were written via the active run's stores.
      var ctx = ActiveRun();
      Assert.AreEqual(6, ctx.Matches.List(1, 100).TotalCount);
      var ratings = ctx.Ratings.GetAll();
      Assert.IsTrue(ratings.Any(r => r.ModelId == "p0" && r.Games > 0));
    }

    [TestMethod]
    public void Start_IsIdempotentWhileRunning() {
      var cfg = Config(swissRounds: 1, cooldown: 0, FourBaselines());
      var block = new TaskCompletionSource();
      var fake = new FakeRunner(block);
      var svc = Build(cfg, fake.Run);

      svc.Start();
      svc.Start(); // second call must be a no-op (no crash / no double loop)
      block.SetResult(); // release the held match

      // No exception is the assertion here; loop finishes on its own.
    }

    // ----- Stop halts scheduling and cancels ------------------------------

    [TestMethod]
    public async Task Stop_CancelsInFlightMatchAndHalts() {
      var cfg = Config(swissRounds: 5, cooldown: 0, FourBaselines());
      var block = new TaskCompletionSource();
      var fake = new FakeRunner(block); // first match blocks until cancelled
      var svc = Build(cfg, fake.Run);

      svc.Start();
      await WaitUntil(() => fake.Observed); // match is in flight
      Assert.AreEqual(RunStatus.Running, svc.GetStatus().Status);

      await svc.StopAsync();

      Assert.AreEqual(RunStatus.Stopped, svc.GetStatus().Status);
      // Only the one (cancelled) table was ever attempted; nothing completed.
      Assert.AreEqual(1, fake.Count);
      Assert.AreEqual(0, ActiveRun().Matches.List(1, 100).TotalCount);
    }

    [TestMethod]
    public async Task Status_ReflectsTransitions() {
      var cfg = Config(swissRounds: 1, cooldown: 0, FourBaselines());
      var svc = Build(cfg, new FakeRunner().Run);

      Assert.AreEqual(RunStatus.Idle, svc.GetStatus().Status);

      svc.Start();
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);
      Assert.AreEqual(RunStatus.Finished, svc.GetStatus().Status);
    }

    // ----- Cooldown honored between matches --------------------------------

    [TestMethod]
    public async Task Cooldown_InvokedBetweenMatches_NotBeforeFirst() {
      // 8 LLMs => 2 tables/round, 2 rounds => 4 matches => 3 gaps.
      var cfg = Config(swissRounds: 2, cooldown: 30, EightLlms());
      int delayCalls = 0;
      var observedDelays = new ConcurrentQueue<TimeSpan>();
      ArenaService.DelayFn delay = (span, token) => {
        Interlocked.Increment(ref delayCalls);
        observedDelays.Enqueue(span);
        return Task.CompletedTask; // zero real wait
      };
      var fake = new FakeRunner();
      var svc = Build(cfg, fake.Run, delay);

      svc.Start();
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);

      Assert.AreEqual(4, fake.Count);
      // Cooldown fires between matches only: 4 matches => 3 cooldowns.
      Assert.AreEqual(3, delayCalls);
      Assert.IsTrue(observedDelays.All(d => d == TimeSpan.FromSeconds(30)));
    }

    [TestMethod]
    public async Task Cooldown_ZeroSeconds_StillInvokesDelaySeamOncePerGap() {
      var cfg = Config(swissRounds: 1, cooldown: 0, EightLlms());
      int delayCalls = 0;
      ArenaService.DelayFn delay = (span, token) => {
        Interlocked.Increment(ref delayCalls);
        return Task.CompletedTask;
      };
      var svc = Build(cfg, new FakeRunner().Run, delay);

      svc.Start();
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);

      // 1 round * 2 tables => 1 gap => exactly one delay-seam invocation.
      Assert.AreEqual(1, delayCalls);
    }

    // ----- Next-match preview ---------------------------------------------

    [TestMethod]
    public async Task NextMatchPreview_ExposedForUpcomingTable() {
      var cfg = Config(swissRounds: 1, cooldown: 0, FourBaselines());
      var block = new TaskCompletionSource();
      var fake = new FakeRunner(block);
      var svc = Build(cfg, fake.Run);

      svc.Start();
      await WaitUntil(() => fake.Observed);

      var preview = svc.GetNextMatchPreview();
      Assert.IsNotNull(preview);
      Assert.AreEqual(4, preview.Seats.Count);
      Assert.AreEqual(1, preview.SwissRound);
      CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 },
          preview.Seats.Select(s => s.Seat).OrderBy(x => x).ToList());

      block.SetResult();
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);
    }

    // ----- Resume from run.json -------------------------------------------

    [TestMethod]
    public async Task Resume_ContinuesFromPersistedRunState() {
      // Pre-seed a run's run.json as if round 0 (2 tables) is done, mid round 1.
      var cfg = Config(swissRounds: 2, cooldown: 0, EightLlms());
      runManager = new RunManager(workspaceDir);
      var ctx = runManager.CreateRun(cfg);
      ctx.RunState.Save(new RunState {
        RunId = ctx.RunId,
        SwissRoundIndex = 1,
        SwissRounds = 2,
        CompletedTablesInRound = 0,
        Status = RunStatus.Stopped,
        StartedAt = DateTime.UtcNow.ToString("o"),
        CompletedPairings = new List<CompletedPairing> {
          new() { SwissRound = 0, MatchId = "m0", ModelIds = new() { "p0", "p1", "p2", "p3" } },
          new() { SwissRound = 0, MatchId = "m1", ModelIds = new() { "p4", "p5", "p6", "p7" } },
        },
      });

      var fake = new FakeRunner();
      var svc = new ArenaService(runManager, () => cfg, tableRunner: fake.Run);

      // On construction it reflects the persisted (crash-safe) state as Stopped.
      Assert.AreEqual(RunStatus.Stopped, svc.GetStatus().Status);
      Assert.AreEqual(ctx.RunId, svc.GetStatus().RunId);

      svc.Start();
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);

      // Only the remaining round (round 1 => 2 tables) is played on resume.
      Assert.AreEqual(2, fake.Count, "resume plays only the unfinished round");
      var final = ctx.RunState.Get();
      Assert.AreEqual(RunStatus.Finished, final.Status);
      Assert.AreEqual(ctx.RunId, final.RunId);
      Assert.AreEqual(4, final.CompletedPairings.Count);
    }

    [TestMethod]
    public async Task RunState_PersistedAfterEachMatch() {
      var cfg = Config(swissRounds: 1, cooldown: 0, EightLlms());
      var fake = new FakeRunner();
      var svc = Build(cfg, fake.Run);

      svc.Start();
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);

      var state = ActiveRun().RunState.Get();
      Assert.IsNotNull(state);
      Assert.AreEqual(RunStatus.Finished, state.Status);
      Assert.AreEqual(2, state.CompletedPairings.Count);
      Assert.IsFalse(string.IsNullOrEmpty(state.RunId));
    }

    [TestMethod]
    public async Task StartNewRun_CreatesSeparateRunPreservingPriorResults() {
      var cfg = Config(swissRounds: 1, cooldown: 0, EightLlms());
      var svc = Build(cfg, new FakeRunner().Run);

      // First run to completion.
      svc.Start();
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);
      var firstRunId = svc.ActiveRunId;
      Assert.AreEqual(2, ActiveRun().Matches.List(1, 100).TotalCount);

      // A brand-new run is a distinct, isolated run.
      await svc.StartNewRunAsync();
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);
      var secondRunId = svc.ActiveRunId;

      Assert.AreNotEqual(firstRunId, secondRunId);
      Assert.AreEqual(2, runManager.RunIds().Count, "two separate runs exist");
      // The first run's matches are untouched by the second run.
      Assert.AreEqual(2, runManager.GetRun(firstRunId).Matches.List(1, 100).TotalCount);
      Assert.AreEqual(2, runManager.GetRun(secondRunId).Matches.List(1, 100).TotalCount);
    }
  }
}
