using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RabiRiichi.Tests.Server.Arena {
  /// <summary>
  /// Persistence + resume tests for <c>run.json</c> (ARENA_DESIGN.md §10/§11).
  /// </summary>
  [TestClass]
  public class RunStateStoreTest {
    private string workspaceDir;

    [TestInitialize]
    public void Setup() {
      workspaceDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_runstate_{Guid.NewGuid():N}");
      Directory.CreateDirectory(workspaceDir);
    }

    [TestCleanup]
    public void Cleanup() {
      if (Directory.Exists(workspaceDir)) {
        try { Directory.Delete(workspaceDir, recursive: true); } catch { }
      }
    }

    private static RunState Sample() => new() {
      RunId = "run-1",
      SwissRoundIndex = 2,
      SwissRounds = 7,
      CompletedTablesInRound = 1,
      Status = RunStatus.Running,
      StartedAt = "2024-01-01T00:00:00Z",
      UpdatedAt = "2024-01-01T00:05:00Z",
      CompletedPairings = new List<CompletedPairing> {
        new() { SwissRound = 0, MatchId = "m0", ModelIds = new() { "a", "b", "c", "d" } },
        new() { SwissRound = 1, MatchId = "m1", ModelIds = new() { "a", "c", "e", "f" } },
      },
    };

    [TestMethod]
    public void Get_MissingFile_ReturnsNull() {
      var store = new RunStateStore(workspaceDir);
      Assert.IsNull(store.Get());
    }

    [TestMethod]
    public void SaveThenGet_RoundTrips() {
      var store = new RunStateStore(workspaceDir);
      store.Save(Sample());

      var got = store.Get();
      Assert.IsNotNull(got);
      Assert.AreEqual("run-1", got.RunId);
      Assert.AreEqual(2, got.SwissRoundIndex);
      Assert.AreEqual(7, got.SwissRounds);
      Assert.AreEqual(1, got.CompletedTablesInRound);
      Assert.AreEqual(RunStatus.Running, got.Status);
      Assert.AreEqual(2, got.CompletedPairings.Count);
      CollectionAssert.AreEqual(
          new[] { "a", "b", "c", "d" }, got.CompletedPairings[0].ModelIds);
    }

    [TestMethod]
    public void Save_PersistsAcrossReload() {
      new RunStateStore(workspaceDir).Save(Sample());

      // A fresh store loads the file on boot (resume from disk).
      var reloaded = new RunStateStore(workspaceDir);
      var got = reloaded.Get();
      Assert.IsNotNull(got);
      Assert.AreEqual("run-1", got.RunId);
      Assert.AreEqual(RunStatus.Running, got.Status);
      Assert.AreEqual("m1", got.CompletedPairings[1].MatchId);
    }

    [TestMethod]
    public void Get_ReturnsDeepCopy_NoCacheMutation() {
      var store = new RunStateStore(workspaceDir);
      store.Save(Sample());

      var a = store.Get();
      a.CompletedPairings.Clear();
      a.RunId = "mutated";

      var b = store.Get();
      Assert.AreEqual("run-1", b.RunId);
      Assert.AreEqual(2, b.CompletedPairings.Count);
    }

    [TestMethod]
    public void Clear_RemovesFileAndCache() {
      var store = new RunStateStore(workspaceDir);
      store.Save(Sample());
      Assert.IsNotNull(store.Get());

      store.Clear();
      Assert.IsNull(store.Get());
      Assert.IsNull(new RunStateStore(workspaceDir).Get());
    }
  }
}
