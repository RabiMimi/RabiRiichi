using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Server.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Server.Arena {
  /// <summary>
  /// Integration-style tests that run a full headless all-BASELINE
  /// (<c>RuleBasedAI</c>) match to completion. No network. See ARENA_DESIGN.md
  /// §10/§11b.
  ///
  /// A full rule-based game is heavy (seconds), so a single shared match is run
  /// once in <see cref="ClassInit"/> and multiple assertions read its result;
  /// only the cheap validation tests run their own (tiny / no) game.
  /// </summary>
  [TestClass]
  public class EvalRoomTest {
    private static string sharedWorkspace;
    private static string sharedReplayDir;
    private static ArenaConfig sharedConfig;
    private static List<EvalSeatAssignment> sharedTable;
    private static EvalRoom sharedRoom;
    private static ReplayStore sharedReplayStore;
    private static EvalResult sharedResult;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _) {
      sharedWorkspace = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_eval_shared_{Guid.NewGuid():N}");
      sharedReplayDir = Path.Combine(sharedWorkspace, "replays");
      Directory.CreateDirectory(sharedReplayDir);

      sharedConfig = BaselineConfig(totalRound: 1);
      sharedTable = Table(sharedConfig);
      sharedReplayStore = new ReplayStore(new ReplayOptions(sharedReplayDir, null));
      var reasoning = new ReasoningStore(sharedWorkspace);
      var usage = new UsageStats(sharedWorkspace);
      sharedRoom = new EvalRoom(sharedConfig, sharedReplayStore, reasoning, usage);

      sharedResult = await sharedRoom.RunAsync(
          sharedTable, gameId: "eval-shared-1", cancellationToken: CancellationToken.None);
    }

    [ClassCleanup]
    public static void ClassCleanup() {
      if (Directory.Exists(sharedWorkspace)) {
        try { Directory.Delete(sharedWorkspace, recursive: true); } catch { }
      }
    }

    private static ArenaConfig BaselineConfig(int totalRound = 1) {
      var cfg = new ArenaConfig {
        WorkspaceDir = "/tmp/arena",
        AdminPassword = "pw",
        ClientUrl = "https://play.example.com",
        WsUrl = "wss://arena.example.com",
      };
      cfg.Run.TotalRound = totalRound;
      cfg.Run.PlayerCount = 4;
      cfg.Decision.TimeoutSeconds = 30;
      cfg.Models.AddRange(new[] {
        Baseline("base-a", 1500),
        Baseline("base-b", 1500),
        Baseline("base-c", 1500),
        Baseline("base-d", 1500),
      });
      return cfg;
    }

    private static ArenaConfig.ModelConfig Baseline(string id, double frozen) => new() {
      Id = id,
      DisplayName = id.ToUpperInvariant(),
      Provider = "baseline",
      Variant = "default",
      FrozenElo = frozen,
      Enabled = true,
    };

    private static List<EvalSeatAssignment> Table(ArenaConfig cfg) =>
        cfg.Models.Select((m, i) => new EvalSeatAssignment { Seat = i, Model = m })
            .ToList();

    // ----- Assertions against the shared finished match --------------------

    [TestMethod]
    public void SharedMatch_FinishesWithValidPlacements() {
      Assert.IsTrue(sharedResult.Completed, "match should run to completion");
      Assert.AreEqual("eval-shared-1", sharedResult.GameId);
      Assert.AreEqual(4, sharedResult.Seats.Count);

      // Placements are a permutation of {1,2,3,4}.
      var placements = sharedResult.Seats.Select(s => s.Placement).OrderBy(p => p).ToList();
      CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, placements);

      // Each seat maps to a distinct roster model id.
      var ids = sharedResult.Seats.Select(s => s.ModelId).OrderBy(x => x).ToList();
      CollectionAssert.AreEqual(new[] { "base-a", "base-b", "base-c", "base-d" }, ids);

      // Points are conserved across the table (4 * 25000 default).
      long total = sharedResult.Seats.Sum(s => s.FinalPoints);
      Assert.AreEqual(100000, total, "points are conserved across the table");
    }

    [TestMethod]
    public void SharedMatch_CapturedRealSeed() {
      // The engine derived a clock seed (non-zero in practice) and it was
      // captured and returned (§11b).
      Assert.AreNotEqual(0UL, sharedResult.Seed, "captured seed should be the real seed");
    }

    [TestMethod]
    public void SharedMatch_SavesWebCompatibleReplayWithStampedSeed() {
      var path = Path.Combine(sharedReplayDir, "eval-shared-1.pb");
      Assert.IsTrue(File.Exists(path), "replay should be saved to disk");

      var saved = sharedReplayStore.GetReplay("eval-shared-1");
      Assert.IsNotNull(saved, "replay should parse as a GameLogMsg (web-compatible)");
      Assert.AreEqual("eval-shared-1", saved.GameId);

      // The seed stamped into the replay equals the captured real seed (§11b
      // end-of-game stamp round-trips), and is non-zero.
      Assert.AreEqual(sharedResult.Seed, saved.Config.Seed,
          "replay config seed must equal the captured real seed");
      Assert.AreNotEqual(0UL, saved.Config.Seed);
      Assert.AreEqual(4, saved.Players.Count);
    }

    [TestMethod]
    public void SharedMatch_ConfigSnapshotHasSeedAndRounds() {
      Assert.IsNotNull(sharedResult.Config, "a config snapshot should be produced");
      // Protobuf JSON encodes 64-bit ints as decimal strings, so seed is a string.
      var seedNode = sharedResult.Config["seed"];
      Assert.IsNotNull(seedNode);
      Assert.AreEqual(sharedResult.Seed, ulong.Parse(seedNode.GetValue<string>()));
      // playerCount / totalRound are int32 -> JSON numbers.
      Assert.AreEqual(1, sharedResult.Config["totalRound"].GetValue<int>());
      Assert.AreEqual(4, sharedResult.Config["playerCount"].GetValue<int>());
    }

    [TestMethod]
    public void SharedMatch_BuildMatchRecordWiresPlacementsAndElo() {
      var store = new RatingStore(sharedWorkspace);
      var rating = new RatingService(sharedConfig.Rating);

      var participants = sharedRoom.BuildRatingParticipants(
          sharedResult, sharedTable, store, sharedConfig.Rating.InitialElo);
      var changes = rating.ApplyMatch(store, participants);

      var record = sharedRoom.BuildMatchRecord(sharedResult, changes,
          matchId: "match-shared", runId: "run-1", swissRound: 2);

      Assert.AreEqual("match-shared", record.MatchId);
      Assert.AreEqual("eval-shared-1", record.GameId);
      Assert.AreEqual("run-1", record.RunId);
      Assert.AreEqual(2, record.SwissRound);
      Assert.AreEqual(unchecked((long)sharedResult.Seed), record.Seed);
      Assert.AreEqual(4, record.Players.Count);

      var recPlacements = record.Players.Select(p => p.Placement).OrderBy(p => p).ToList();
      CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, recPlacements);

      // Frozen baselines stay at 1500.
      foreach (var p in record.Players) {
        Assert.AreEqual(1500.0, p.EloAfter, 1e-9);
      }

      // The record appends cleanly to a MatchStore.
      var matchStore = new MatchStore(sharedWorkspace);
      matchStore.Append(record);
      Assert.IsNotNull(matchStore.Get("match-shared"));
    }

    // ----- Cheap validation tests (no full game) ---------------------------

    [TestMethod]
    public async Task Cancellation_BeforeRun_StopsWithoutCompletion() {
      var cfg = BaselineConfig(totalRound: 1);
      var replayDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_eval_cancel_{Guid.NewGuid():N}");
      Directory.CreateDirectory(replayDir);
      try {
        var room = new EvalRoom(cfg, new ReplayStore(new ReplayOptions(replayDir, null)));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await room.RunAsync(Table(cfg), gameId: "eval-cancel",
            cancellationToken: cts.Token);
        Assert.IsFalse(result.Completed);
      } finally {
        try { Directory.Delete(replayDir, recursive: true); } catch { }
      }
    }

    [TestMethod]
    public async Task RunAsync_RejectsWrongSeatCount() {
      var cfg = BaselineConfig(totalRound: 1);
      var room = new EvalRoom(cfg, new ReplayStore(new ReplayOptions(null, null)));
      var bad = Table(cfg).Take(3).ToList();

      await Assert.ThrowsExceptionAsync<ArgumentException>(
          () => room.RunAsync(bad, gameId: "eval-bad"));
    }
  }
}
