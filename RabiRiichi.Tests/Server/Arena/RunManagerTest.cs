using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Storage;
using System;
using System.IO;
using System.Linq;

namespace RabiRiichi.Tests.Server.Arena {
  /// <summary>
  /// Tests for <see cref="RunManager"/> (ARENA_DESIGN.md §10/§11, multi-run): run
  /// creation freezes an isolated config snapshot, runs get their own stores, the
  /// newest-first ordering is stable across reloads, and later config edits never
  /// bleed into an existing run.
  /// </summary>
  [TestClass]
  public class RunManagerTest {
    private string workspaceDir;

    [TestInitialize]
    public void Setup() {
      workspaceDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_runmgr_{Guid.NewGuid():N}");
      Directory.CreateDirectory(workspaceDir);
    }

    [TestCleanup]
    public void Cleanup() {
      if (Directory.Exists(workspaceDir)) {
        try { Directory.Delete(workspaceDir, recursive: true); } catch { }
      }
    }

    private static ArenaConfig MakeConfig(int swissRounds = 3) {
      var cfg = new ArenaConfig { WorkspaceDir = "ws", AdminPassword = "pw" };
      cfg.Run.SwissRounds = swissRounds;
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "a", DisplayName = "A", Provider = "baseline", FrozenElo = 1500,
      });
      return cfg;
    }

    [TestMethod]
    public void CreateRun_WritesIsolatedSnapshotAndStores() {
      var mgr = new RunManager(workspaceDir);
      var ctx = mgr.CreateRun(MakeConfig(swissRounds: 5));

      Assert.IsTrue(Directory.Exists(ctx.Dir));
      Assert.IsTrue(File.Exists(Path.Combine(ctx.Dir, "config.json")));
      Assert.AreEqual(5, ctx.Config.Run.SwissRounds);
      // Stores are rooted in the run dir, not the workspace root.
      ctx.Ratings.Update(new RatingRecord { ModelId = "a", Elo = 1500 });
      Assert.IsTrue(File.Exists(Path.Combine(ctx.Dir, "ratings.json")));
      Assert.IsFalse(File.Exists(Path.Combine(workspaceDir, "ratings.json")));
    }

    [TestMethod]
    public void GetRun_ReturnsCachedContext() {
      var mgr = new RunManager(workspaceDir);
      var ctx = mgr.CreateRun(MakeConfig());
      Assert.AreSame(ctx, mgr.GetRun(ctx.RunId));
      Assert.IsNull(mgr.GetRun("no-such-run"));
    }

    [TestMethod]
    public void NewestRunId_IsMostRecentlyCreated_AndSurvivesReload() {
      var mgr = new RunManager(workspaceDir);
      var first = mgr.CreateRun(MakeConfig());
      var second = mgr.CreateRun(MakeConfig());
      Assert.AreEqual(second.RunId, mgr.NewestRunId());
      CollectionAssert.AreEqual(
          new[] { second.RunId, first.RunId }, mgr.RunIds().ToArray());

      // A fresh manager over the same workspace reloads the same ordering.
      var reloaded = new RunManager(workspaceDir);
      Assert.AreEqual(second.RunId, reloaded.NewestRunId());
      Assert.AreEqual(2, reloaded.RunIds().Count);
    }

    [TestMethod]
    public void Snapshot_IsIndependentOfLaterConfigEdits() {
      var mgr = new RunManager(workspaceDir);
      var cfg = MakeConfig(swissRounds: 2);
      var ctx = mgr.CreateRun(cfg);

      // Mutate the source config AFTER creating the run.
      cfg.Run.SwissRounds = 99;
      cfg.Models.Clear();

      Assert.AreEqual(2, ctx.Config.Run.SwissRounds, "snapshot is frozen");
      Assert.AreEqual(1, ctx.Config.Models.Count);
    }

    [TestMethod]
    public void ListRuns_ProjectsSummariesNewestFirst() {
      var mgr = new RunManager(workspaceDir);
      var first = mgr.CreateRun(MakeConfig(swissRounds: 4));
      var second = mgr.CreateRun(MakeConfig(swissRounds: 4));
      // Give the second run some persisted progress.
      second.RunState.Save(new RunState {
        RunId = second.RunId,
        SwissRoundIndex = 1,
        SwissRounds = 4,
        Status = RunStatus.Running,
      });

      var summaries = mgr.ListRuns();
      Assert.AreEqual(2, summaries.Count);
      Assert.AreEqual(second.RunId, summaries[0].RunId);
      Assert.AreEqual("Running", summaries[0].Status);
      Assert.AreEqual(2, summaries[0].CurrentSwissRound);
      Assert.AreEqual(first.RunId, summaries[1].RunId);
      Assert.AreEqual(1, summaries[0].ModelCount);
    }
  }
}
