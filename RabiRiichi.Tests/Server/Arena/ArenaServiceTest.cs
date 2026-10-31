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
  /// Orchestrator tests for <see cref="ArenaService"/> covering Swiss, Balanced,
  /// and Custom scheduling, parallel <c>MatchConcurrency</c>, graceful <c>Pause</c>
  /// and <c>Resume</c>, immediate <c>StopAsync</c>, <c>AppendBatch</c>,
  /// <c>ActivateRun</c>, and <c>DeleteRun</c>.
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
      Variant = "balanced",
      FrozenElo = frozen,
      Enabled = true,
    };

    private ArenaConfig Config(
        int swissRounds, int concurrency = 1, params ArenaConfig.ModelConfig[] models) {
      var cfg = new ArenaConfig {
        WorkspaceDir = workspaceDir,
        AdminPassword = "pw",
        ClientUrl = "https://play.example.com",
        WsUrl = "wss://arena.example.com",
      };
      cfg.Run.SwissRounds = swissRounds;
      cfg.Run.MatchConcurrency = concurrency;
      cfg.GameRules.PlayerCount = 4;
      cfg.GameRules.TotalRound = 1;
      cfg.Models.AddRange(models);
      return cfg;
    }

    private static ArenaConfig.ModelConfig[] EightLlms() =>
        Enumerable.Range(0, 8).Select(i => Llm($"p{i}")).ToArray();

    private static ArenaConfig.ModelConfig[] FourBaselines() =>
        new[] { Baseline("a"), Baseline("b"), Baseline("c"), Baseline("d") };

    private sealed class FakeRunner {
      public readonly ConcurrentQueue<string> Ran = new();
      public int Count => Ran.Count;
      public volatile bool Observed;
      public int CurrentInFlight;
      public int MaxObservedConcurrency;

      private readonly TaskCompletionSource block;

      public FakeRunner(TaskCompletionSource block = null) {
        this.block = block;
      }

      public async Task<EvalResult> Run(
          SwissTable table, string gameId, CancellationToken token) {
        Observed = true;
        Ran.Enqueue(gameId);
        int cur = Interlocked.Increment(ref CurrentInFlight);
        int prevMax;
        do {
          prevMax = Volatile.Read(ref MaxObservedConcurrency);
          if (cur <= prevMax) break;
        } while (Interlocked.CompareExchange(ref MaxObservedConcurrency, cur, prevMax) != prevMax);

        try {
          if (block != null) {
            using var reg = token.Register(() => block.TrySetCanceled(token));
            await block.Task;
          } else {
            await Task.Yield();
          }
          token.ThrowIfCancellationRequested();
          var seats = table.Assignments.Select((a, i) => new EvalSeatResult {
            Seat = a.Seat,
            ModelId = a.Model.Id,
            DisplayName = a.Model.DisplayName,
            Provider = a.Model.Provider,
            FinalPoints = 25000 + (4 - i) * 1000 - 2500,
            Placement = i + 1,
            RoundsPlayed = 4,
            PenaltyCount = 0,
          }).ToList();
          return new EvalResult {
            GameId = gameId,
            Seed = 12345,
            Seats = seats,
            PlayerCount = table.Assignments.Count,
            TotalRound = 1,
            RoundsPlayed = 4,
            StartedAt = DateTime.UtcNow.ToString("o"),
            FinishedAt = DateTime.UtcNow.ToString("o"),
            Completed = true,
          };
        } finally {
          Interlocked.Decrement(ref CurrentInFlight);
        }
      }
    }

    private ArenaService Build(
        ArenaConfig cfg, Func<SwissTable, string, CancellationToken, Task<EvalResult>> runner) {
      runManager = new RunManager(workspaceDir);
      return new ArenaService(runManager, () => cfg, simpleRunner: runner);
    }

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

    [TestMethod]
    public async Task Start_RunsAllTablesOfAllRounds() {
      var cfg = Config(swissRounds: 3, concurrency: 1, EightLlms());
      var fake = new FakeRunner();
      var svc = Build(cfg, fake.Run);

      svc.Start();
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);

      Assert.AreEqual(6, fake.Count, "3 rounds * 2 tables");
      Assert.AreEqual(6, svc.GetStatus().CompletedMatches);

      var ctx = ActiveRun();
      Assert.AreEqual(6, ctx.Matches.List(1, 100).TotalCount);
      Assert.IsTrue(ctx.Ratings.GetAll().Any(r => r.ModelId == "p0" && r.Games > 0));
    }

    [TestMethod]
    public async Task ParallelConcurrency_ExecutesMultipleTablesInFlightSimultaneously() {
      var cfg = Config(swissRounds: 1, concurrency: 2, EightLlms());
      var gate = new TaskCompletionSource();
      var fake = new FakeRunner(gate);
      var svc = Build(cfg, fake.Run);

      svc.Start();
      await WaitUntil(() => Volatile.Read(ref fake.CurrentInFlight) == 2);
      Assert.AreEqual(2, fake.MaxObservedConcurrency);

      gate.SetResult();
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);
      Assert.AreEqual(2, svc.GetStatus().CompletedMatches);
    }

    [TestMethod]
    public async Task PauseAndResume_FinishesInFlightTableThenResumesRemainingRounds() {
      var cfg = Config(swissRounds: 2, concurrency: 1, FourBaselines());
      var firstTableGate = new TaskCompletionSource();
      int callIdx = 0;

      runManager = new RunManager(workspaceDir);
      var svc = new ArenaService(runManager, () => cfg, simpleRunner: async (table, gid, ct) => {
        int idx = Interlocked.Increment(ref callIdx);
        if (idx == 1) {
          await firstTableGate.Task;
        }
        return new EvalResult {
          GameId = gid,
          Seed = 999,
          PlayerCount = 4,
          TotalRound = 1,
          RoundsPlayed = 4,
          Completed = true,
          Seats = table.Assignments.Select((a, i) => new EvalSeatResult {
            Seat = a.Seat, ModelId = a.Model.Id, DisplayName = a.Model.DisplayName,
            FinalPoints = 25000, Placement = i + 1,
          }).ToList(),
        };
      });

      svc.Start();
      await WaitUntil(() => Volatile.Read(ref callIdx) == 1);

      // Request graceful pause while match #1 is mid-flight.
      svc.Pause();
      firstTableGate.SetResult();

      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Paused);
      Assert.AreEqual(1, svc.GetStatus().CompletedMatches, "in-flight match completed before pausing");
      Assert.AreEqual(1, Volatile.Read(ref callIdx), "round 2 was not launched while paused");

      // Resume finishes round 2!
      svc.Resume();
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);
      Assert.AreEqual(2, svc.GetStatus().CompletedMatches);
    }

    [TestMethod]
    public async Task Stop_CancelsInFlightMatchImmediately() {
      var cfg = Config(swissRounds: 5, concurrency: 1, FourBaselines());
      var block = new TaskCompletionSource();
      var fake = new FakeRunner(block);
      var svc = Build(cfg, fake.Run);

      svc.Start();
      await WaitUntil(() => fake.Observed);
      Assert.AreEqual(RunStatus.Running, svc.GetStatus().Status);

      await svc.StopAsync();

      Assert.AreEqual(RunStatus.Stopped, svc.GetStatus().Status);
      Assert.AreEqual(1, fake.Count);
      Assert.AreEqual(0, ActiveRun().Matches.List(1, 100).TotalCount);
    }

    [TestMethod]
    public async Task AppendBatch_SupportsBalancedMatchesSwissRoundsAndCustomLineups() {
      var cfg = Config(swissRounds: 1, concurrency: 2, FourBaselines());
      var fake = new FakeRunner();
      var svc = Build(cfg, fake.Run);

      // Start initial 1-round Swiss run (1 match).
      svc.Start();
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);
      Assert.AreEqual(1, svc.GetStatus().CompletedMatches);

      // Append 2 balanced matches to the finished run -> auto-resumes to 3 matches.
      var err1 = svc.AppendBatch(new AppendBatchOptions {
        Mode = "balanced_matches",
        Count = 2,
        Label = "Balanced +2",
      });
      Assert.AreEqual(0, err1.Count);
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);
      Assert.AreEqual(3, svc.GetStatus().CompletedMatches);

      // Append 2 custom seat-specific matches -> auto-resumes to 5 matches.
      var err2 = svc.AppendBatch(new AppendBatchOptions {
        Mode = "custom_lineup",
        Count = 2,
        Label = "Showmatch",
        SeatModelIds = new List<string> { "d", "c", "b", "a" },
      });
      Assert.AreEqual(0, err2.Count);
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);
      Assert.AreEqual(5, svc.GetStatus().CompletedMatches);
    }

    [TestMethod]
    public async Task ActivateAndDeleteRun_SwitchesActiveRunAndDeletesSafely() {
      var cfg = Config(swissRounds: 1, concurrency: 1, FourBaselines());
      var svc = Build(cfg, new FakeRunner().Run);

      await svc.StartNewRunAsync(new StartRunOptions { Label = "Run One" });
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);
      var run1 = svc.ActiveRunId;

      await svc.StartNewRunAsync(new StartRunOptions { Label = "Run Two" });
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);
      var run2 = svc.ActiveRunId;

      Assert.AreNotEqual(run1, run2);
      Assert.IsTrue(svc.ActivateRun(run1, out var actErr), actErr);
      Assert.AreEqual(run1, svc.ActiveRunId);

      Assert.IsTrue(svc.DeleteRun(run2, out var delErr), delErr);
      Assert.AreEqual(1, runManager.ListRuns().Count);
    }

    [TestMethod]
    public async Task Resume_ContinuesFromPersistedRunState() {
      var cfg = Config(swissRounds: 2, concurrency: 1, EightLlms());
      runManager = new RunManager(workspaceDir);
      var ctx = runManager.CreateRun(cfg);
      ctx.RunState.Save(new RunState {
        RunId = ctx.RunId,
        ScheduleMode = "swiss",
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
      var svc = new ArenaService(runManager, () => cfg, simpleRunner: fake.Run);

      Assert.AreEqual(RunStatus.Stopped, svc.GetStatus().Status);
      Assert.AreEqual(ctx.RunId, svc.GetStatus().RunId);

      svc.Start();
      await WaitUntil(() => svc.GetStatus().Status == RunStatus.Finished);

      Assert.AreEqual(2, fake.Count, "resume plays only the unfinished round");
      Assert.AreEqual(4, ctx.RunState.Get().CompletedPairings.Count);
    }
  }
}
