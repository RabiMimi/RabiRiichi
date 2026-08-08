using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace RabiRiichi.Tests.Server.Arena {
  [TestClass]
  public class MatchStoreTest {
    private string workspaceDir;

    [TestInitialize]
    public void Setup() {
      workspaceDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_match_{Guid.NewGuid():N}");
      Directory.CreateDirectory(workspaceDir);
    }

    [TestCleanup]
    public void Cleanup() {
      if (Directory.Exists(workspaceDir)) {
        try {
          Directory.Delete(workspaceDir, recursive: true);
        } catch {
          // ignore
        }
      }
    }

    private static MatchRecord MakeRecord(string matchId, int order) {
      return new MatchRecord {
        MatchId = matchId,
        GameId = "game-" + matchId,
        RunId = "run-1",
        SwissRound = 1,
        StartedAt = "2024-01-01T00:00:00Z",
        FinishedAt = $"2024-01-01T00:0{order}:00Z",
        Seed = 12345 + order,
        Config = new JsonObject {
          ["playerCount"] = 4,
          ["totalRound"] = 2,
        },
        Players = new List<MatchPlayer> {
          new() { Seat = 0, ModelId = "gpt-x", DisplayName = "GPT-X",
                  FinalPoints = 40000, Placement = 1, EloBefore = 1500, EloAfter = 1512 },
          new() { Seat = 1, ModelId = "baseline-mid", DisplayName = "Rule (mid)",
                  FinalPoints = 30000, Placement = 2, EloBefore = 1500, EloAfter = 1500 },
          new() { Seat = 2, ModelId = "gemini-x", DisplayName = "Gemini-X",
                  FinalPoints = 20000, Placement = 3, EloBefore = 1500, EloAfter = 1494 },
          new() { Seat = 3, ModelId = "baseline-weak", DisplayName = "Rule (weak)",
                  FinalPoints = 10000, Placement = 4, EloBefore = 1400, EloAfter = 1400 },
        },
      };
    }

    [TestMethod]
    public void TestAppendAndGetById() {
      var store = new MatchStore(workspaceDir);
      var record = MakeRecord("m1", 1);
      store.Append(record);

      var got = store.Get("m1");
      Assert.IsNotNull(got);
      Assert.AreEqual("m1", got.MatchId);
      Assert.AreEqual("game-m1", got.GameId);
      Assert.AreEqual(12346, got.Seed);
      Assert.AreEqual(4, got.Players.Count);
      Assert.AreEqual(1, got.Players.Single(p => p.ModelId == "gpt-x").Placement);
      Assert.IsNotNull(got.Config);
      Assert.AreEqual(4, (int)got.Config["playerCount"]);
    }

    [TestMethod]
    public void TestGetMissingReturnsNull() {
      var store = new MatchStore(workspaceDir);
      Assert.IsNull(store.Get("nope"));
    }

    [TestMethod]
    public void TestListNewestFirstAndPaging() {
      var store = new MatchStore(workspaceDir);
      // Append 5 in chronological order; index should be reverse-chronological.
      for (int i = 1; i <= 5; i++) {
        store.Append(MakeRecord("m" + i, i));
      }

      var page1 = store.List(1, 2);
      Assert.AreEqual(5, page1.TotalCount);
      Assert.AreEqual(2, page1.Items.Count);
      // Newest (m5, appended last) is first.
      Assert.AreEqual("m5", page1.Items[0].MatchId);
      Assert.AreEqual("m4", page1.Items[1].MatchId);

      var page2 = store.List(2, 2);
      Assert.AreEqual("m3", page2.Items[0].MatchId);
      Assert.AreEqual("m2", page2.Items[1].MatchId);

      var page3 = store.List(3, 2);
      Assert.AreEqual(1, page3.Items.Count);
      Assert.AreEqual("m1", page3.Items[0].MatchId);

      // Out-of-range page.
      var page4 = store.List(4, 2);
      Assert.AreEqual(0, page4.Items.Count);
      Assert.AreEqual(5, page4.TotalCount);
    }

    [TestMethod]
    public void TestIndexSummaryHasEloDelta() {
      var store = new MatchStore(workspaceDir);
      store.Append(MakeRecord("m1", 1));

      var page = store.List(1, 10);
      var entry = page.Items[0];
      var winner = entry.Players.Single(p => p.DisplayName == "GPT-X");
      Assert.AreEqual(1, winner.Placement);
      Assert.AreEqual(12, winner.EloDelta); // 1512 - 1500
      Assert.AreEqual(1512, winner.EloAfter);
    }

    [TestMethod]
    public void TestIndexPersistsAcrossReload() {
      var store = new MatchStore(workspaceDir);
      store.Append(MakeRecord("m1", 1));
      store.Append(MakeRecord("m2", 2));

      // New instance loads the persisted index.
      var reloaded = new MatchStore(workspaceDir);
      var page = reloaded.List(1, 10);
      Assert.AreEqual(2, page.TotalCount);
      Assert.AreEqual("m2", page.Items[0].MatchId);
      Assert.IsNotNull(reloaded.Get("m1"));
    }

    [TestMethod]
    public void TestReAppendReplacesAndDeduplicates() {
      var store = new MatchStore(workspaceDir);
      store.Append(MakeRecord("m1", 1));
      // Append again with the same id; index must not grow.
      store.Append(MakeRecord("m1", 9));

      var page = store.List(1, 10);
      Assert.AreEqual(1, page.TotalCount);
      Assert.AreEqual(1, page.Items.Count(e => e.MatchId == "m1"));
      Assert.AreEqual(12354, store.Get("m1").Seed); // 12345 + 9
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void TestAppendRejectsBadId() {
      var store = new MatchStore(workspaceDir);
      var bad = MakeRecord("../evil", 1);
      store.Append(bad);
    }
  }
}
