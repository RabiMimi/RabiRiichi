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
  /// Full headless integration tests for <see cref="EvalRoom"/> covering all 5
  /// <c>ArenaBaselineAI</c> profiles, post-game replay seed &amp; DisplayName
  /// stamping, round/Mahjong telemetry tracking, and Wildcard + Classical Koyaku
  /// non-standard Sanma matches.
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

      sharedConfig = BaselineConfig(sharedWorkspace, totalRound: 1);
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

    private static ArenaConfig BaselineConfig(string wsDir, int totalRound = 1) {
      var cfg = new ArenaConfig {
        WorkspaceDir = wsDir,
        AdminPassword = "pw",
        ClientUrl = "https://play.example.com",
        WsUrl = "wss://arena.example.com",
      };
      cfg.GameRules.TotalRound = totalRound;
      cfg.GameRules.PlayerCount = 4;
      cfg.Prompts.RevealOpponentIdentity = false; // Anonymous mid-game, stamped post-game!
      cfg.Decision.TimeoutSeconds = 30;
      cfg.Models.AddRange(new[] {
        Baseline("base-a", "balanced", 1500),
        Baseline("base-b", "aggressive", 1500),
        Baseline("base-c", "defensive", 1500),
        Baseline("base-d", "fast_open", 1500),
      });
      return cfg;
    }

    private static ArenaConfig.ModelConfig Baseline(string id, string variant, double? frozen) => new() {
      Id = id,
      DisplayName = id.ToUpperInvariant(),
      Provider = "baseline",
      Variant = variant,
      FrozenElo = frozen,
      Enabled = true,
    };

    private static List<EvalSeatAssignment> Table(ArenaConfig cfg) =>
        cfg.Models.Select((m, i) => new EvalSeatAssignment { Seat = i, Model = m })
            .ToList();

    // ----- Assertions against the shared finished match --------------------

    [TestMethod]
    public void SharedMatch_FinishesWithValidPlacementsAndRoundSummaries() {
      Assert.IsTrue(sharedResult.Completed, "match should run to completion");
      Assert.AreEqual("eval-shared-1", sharedResult.GameId);
      Assert.AreEqual(4, sharedResult.Seats.Count);
      Assert.IsTrue(sharedResult.RoundsPlayed >= 1);
      Assert.AreEqual(sharedResult.RoundsPlayed, sharedResult.Rounds.Count);

      var placements = sharedResult.Seats.Select(s => s.Placement).OrderBy(p => p).ToList();
      CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, placements);

      var ids = sharedResult.Seats.Select(s => s.ModelId).OrderBy(x => x).ToList();
      CollectionAssert.AreEqual(new[] { "base-a", "base-b", "base-c", "base-d" }, ids);

      long total = sharedResult.Seats.Sum(s => s.FinalPoints);
      Assert.AreEqual(100000, total, "points are conserved across the table");
    }

    [TestMethod]
    public void SharedMatch_SavesWebCompatibleReplayWithStampedSeedAndRealDisplayNames() {
      var path = Path.Combine(sharedReplayDir, "eval-shared-1.pb");
      Assert.IsTrue(File.Exists(path), "replay should be saved to disk");

      var saved = sharedReplayStore.GetReplay("eval-shared-1");
      Assert.IsNotNull(saved, "replay should parse as a GameLogMsg (web-compatible)");
      Assert.AreEqual("eval-shared-1", saved.GameId);

      Assert.AreNotEqual(0UL, sharedResult.Seed);
      Assert.AreEqual(sharedResult.Seed, saved.Config.Seed);
      Assert.AreEqual(4, saved.Players.Count);

      // Even though RevealOpponentIdentity=false hid names mid-game, and Room shuffles
      // seats on game start, every seat in the saved 3D replay must display the real
      // DisplayName of the contender that played in that seat.
      var expectedBySeat = sharedResult.Seats.OrderBy(s => s.Seat).Select(s => s.DisplayName).ToList();
      var nicknames = saved.Players.OrderBy(p => p.Seat).Select(p => p.Nickname).ToList();
      CollectionAssert.AreEqual(expectedBySeat, nicknames);
      CollectionAssert.AreEquivalent(new[] { "BASE-A", "BASE-B", "BASE-C", "BASE-D" }, nicknames);
    }

    [TestMethod]
    public void SharedMatch_ConfigSnapshotHasSeedAndRounds() {
      Assert.IsNotNull(sharedResult.Config);
      var seedNode = sharedResult.Config["seed"];
      Assert.IsNotNull(seedNode);
      Assert.AreEqual(sharedResult.Seed, ulong.Parse(seedNode.GetValue<string>()));
      Assert.AreEqual(1, sharedResult.Config["totalRound"].GetValue<int>());
      Assert.AreEqual(4, sharedResult.Config["playerCount"].GetValue<int>());
    }

    [TestMethod]
    public void SharedMatch_BuildMatchRecordWiresPlacementsEloAndMahjongStats() {
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
      Assert.AreEqual(sharedResult.RoundsPlayed, record.RoundsPlayed);

      foreach (var p in record.Players) {
        Assert.AreEqual(1500.0, p.EloAfter, 1e-9);
        Assert.AreEqual(sharedResult.RoundsPlayed, p.RoundsPlayed);
      }

      var matchStore = new MatchStore(sharedWorkspace);
      matchStore.Append(record);
      Assert.IsNotNull(matchStore.Get("match-shared"));
    }

    // ----- Wildcard + Classical Koyaku + Sanma headless integration --------

    [TestMethod]
    [Timeout(60_000)]
    public async Task WildcardSanmaKoyakuMatch_RunsToCompletionAndSavesReplay() {
      var ws = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_eval_wild_{Guid.NewGuid():N}");
      var replayDir = Path.Combine(ws, "replays");
      Directory.CreateDirectory(replayDir);
      try {
        var cfg = new ArenaConfig {
          WorkspaceDir = ws,
          AdminPassword = "pw",
        };
        cfg.GameRules.PlayerCount = 3;
        cfg.GameRules.TotalRound = 1;
        cfg.GameRules.TileSet.Preset = "Sanma";
        cfg.GameRules.TileSet.ExtraWildcards = 2;
        cfg.GameRules.AllowedYakus.Add("TsubameGaeshi");
        cfg.GameRules.AllowedYakus.Add("Kanburi");
        cfg.Models.AddRange(new[] {
          Baseline("sanma-bal", "balanced", null),
          Baseline("sanma-agg", "aggressive", null),
          Baseline("sanma-tsumo", "tsumogiri", 1350),
        });

        var replayStore = new ReplayStore(new ReplayOptions(replayDir, null));
        var room = new EvalRoom(cfg, replayStore, new ReasoningStore(ws), new UsageStats(ws));
        var progressObserved = 0;

        var res = await room.RunAsync(
            Table(cfg),
            gameId: "eval-wild-sanma-1",
            cancellationToken: CancellationToken.None,
            onProgress: _ => Interlocked.Increment(ref progressObserved));

        Assert.IsTrue(res.Completed);
        Assert.AreEqual(3, res.PlayerCount);
        Assert.AreEqual("Sanma", res.TileSetPreset);
        Assert.IsTrue(res.HasWildcards);
        Assert.AreEqual(2, res.WildcardCount);
        Assert.IsTrue(progressObserved > 0, "live progress callback should fire during play");

        var saved = replayStore.GetReplay("eval-wild-sanma-1");
        Assert.IsNotNull(saved);
        Assert.AreEqual(3, saved.Players.Count);
        foreach (var seatRes in res.Seats) {
          Assert.AreEqual(
              seatRes.DisplayName,
              saved.Players.Single(p => p.Seat == seatRes.Seat).Nickname);
        }
      } finally {
        try { Directory.Delete(ws, recursive: true); } catch { }
      }
    }

    // ----- Cheap validation tests ------------------------------------------

    [TestMethod]
    public async Task Cancellation_BeforeRun_StopsWithoutCompletion() {
      var replayDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_eval_cancel_{Guid.NewGuid():N}");
      Directory.CreateDirectory(replayDir);
      try {
        var cfg = BaselineConfig(replayDir, totalRound: 1);
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
      var cfg = BaselineConfig(sharedWorkspace, totalRound: 1);
      var room = new EvalRoom(cfg, new ReplayStore(new ReplayOptions(null, null)));
      var bad = Table(cfg).Take(3).ToList();

      await Assert.ThrowsExceptionAsync<ArgumentException>(
          () => room.RunAsync(bad, gameId: "eval-bad"));
    }
  }
}
