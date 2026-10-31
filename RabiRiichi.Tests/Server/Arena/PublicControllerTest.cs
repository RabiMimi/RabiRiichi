using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Controllers;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Generated.Core;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace RabiRiichi.Tests.Server.Arena {
  /// <summary>
  /// Tests for <see cref="PublicController"/> and <see cref="ArenaAnalyticsService"/>:
  /// metadata catalog, rich leaderboard, multi-run analytics (Elo history, H2H,
  /// yaku breakdown), match history + seat replay links, protobuf replay download,
  /// public reasoning privacy gate, and replay reasoning track.
  /// </summary>
  [TestClass]
  public class PublicControllerTest {
    private string workspaceDir;
    private RunManager runManager;
    private RunContext ctx;
    private ReasoningStore reasoningStore;
    private ReplayStore replayStore;

    [TestInitialize]
    public void Setup() {
      workspaceDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_pub_{Guid.NewGuid():N}");
      Directory.CreateDirectory(workspaceDir);
    }

    [TestCleanup]
    public void Cleanup() {
      if (Directory.Exists(workspaceDir)) {
        try { Directory.Delete(workspaceDir, recursive: true); } catch { }
      }
    }

    private ArenaConfig MakeConfig() {
      var cfg = new ArenaConfig {
        WorkspaceDir = workspaceDir,
        AdminPassword = "pw",
        ClientUrl = "https://play.example.com",
        WsUrl = "wss://arena.example.com",
      };
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "gpt-x", DisplayName = "GPT-X", Provider = "openai", Model = "m",
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "gemini-x", DisplayName = "Gemini-X", Provider = "gemini", Model = "m",
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "baseline-mid", DisplayName = "Rule (mid)", Provider = "baseline",
        Variant = "balanced", FrozenElo = 1500,
      });
      return cfg;
    }

    private PublicController MakeController(ArenaConfig cfg) {
      runManager = new RunManager(workspaceDir);
      ctx = runManager.CreateRun(cfg);
      var arenaService = new ArenaService(runManager, () => cfg);
      reasoningStore = new ReasoningStore(workspaceDir);
      var replayDir = Path.Combine(workspaceDir, "replays");
      Directory.CreateDirectory(replayDir);
      replayStore = new ReplayStore(new ReplayOptions(replayDir, null));
      return new PublicController(cfg, runManager, arenaService, reasoningStore, replayStore);
    }

    private static string ReplayLink(ArenaConfig cfg, string gameId, int? seat = null) {
      var baseUrl =
          $"{cfg.ClientUrl}?server={Uri.EscapeDataString(cfg.WsUrl)}&replay={Uri.EscapeDataString(gameId)}&reasoning=1";
      return seat.HasValue ? $"{baseUrl}&seat={seat.Value}" : baseUrl;
    }

    private static MatchRecord MakeRecord(string matchId, int order, string runId = "run-1") {
      var gameId = "game-" + matchId;
      return new MatchRecord {
        MatchId = matchId,
        GameId = gameId,
        RunId = runId,
        SwissRound = 1,
        StartedAt = "2025-01-01T00:00:00Z",
        FinishedAt = $"2025-01-01T00:0{order}:00Z",
        DurationMs = 1500,
        PlayerCount = 4,
        TotalRound = 1,
        TileSetPreset = "RegularWildcards",
        HasWildcards = true,
        WildcardCount = 4,
        RoundsPlayed = 4,
        Seed = 777_000 + order,
        Config = new JsonObject { ["playerCount"] = 4, ["totalRound"] = 1, ["seed"] = 777_000 + order },
        Players = new List<MatchPlayer> {
          new() {
            Seat = 0, ModelId = "gpt-x", DisplayName = "GPT-X", Provider = "openai",
            FinalPoints = 40000, Placement = 1, EloBefore = 1500, EloAfter = 1512,
            RoundsPlayed = 4, AgariCount = 2, TsumoCount = 1, RonCount = 1,
            TotalWinPoints = 16000, TotalWinHan = 7, RiichiCount = 2, WildcardWinCount = 1,
            YakuCounts = new Dictionary<string, int> { ["Riichi"] = 2, ["TsubameGaeshi"] = 1 },
            DecisionsCount = 20, ValidFirstTryCount = 19, RetryCount = 1,
            PromptTokens = 5000, CompletionTokens = 800, TotalLatencyMs = 8000,
          },
          new() {
            Seat = 1, ModelId = "gemini-x", DisplayName = "Gemini-X", Provider = "gemini",
            FinalPoints = 20000, Placement = 3, EloBefore = 1500, EloAfter = 1494,
            RoundsPlayed = 4, DealInCount = 1, DealInPoints = 8000,
            DecisionsCount = 18, ValidFirstTryCount = 18,
          },
          new() {
            Seat = 2, ModelId = "baseline-mid", DisplayName = "Rule (mid)", Provider = "baseline",
            FinalPoints = 25000, Placement = 2, EloBefore = 1500, EloAfter = 1500,
            RoundsPlayed = 4,
          },
          new() {
            Seat = 3, ModelId = "baseline-mid", DisplayName = "Rule (mid)", Provider = "baseline",
            FinalPoints = 15000, Placement = 4, EloBefore = 1500, EloAfter = 1500,
            RoundsPlayed = 4,
          },
        },
      };
    }

    private static T Body<T>(ActionResult<T> result) {
      if (result.Result is OkObjectResult ok) {
        return (T)ok.Value;
      }
      return result.Value;
    }

    [TestMethod]
    public void TestMetadataEndpoint_ExposesCatalogAndTilePresets() {
      var cfg = MakeConfig();
      var controller = MakeController(cfg);

      var meta = Body(controller.GetMetadata());
      Assert.IsNotNull(meta);
      Assert.IsTrue(meta.Providers.Count >= 5);
      Assert.AreEqual(73, meta.Yakus.Count);
      Assert.IsTrue(meta.PresetTileCounts.ContainsKey("RegularWildcards"));
      Assert.AreEqual(4, meta.PresetTileCounts["RegularWildcards"]["*"]);
    }

    [TestMethod]
    public void TestLeaderboardAndAnalytics_AggregatesMahjongYakuH2hAndEloSeries() {
      var cfg = MakeConfig();
      var controller = MakeController(cfg);
      ctx.Ratings.Update(new RatingRecord {
        ModelId = "gpt-x", Elo = 1560, Games = 2, Wins = 2,
        Place1 = 2, Place2 = 0, Place3 = 0, Place4 = 0, Penalties = 0,
      });
      ctx.Ratings.Update(new RatingRecord {
        ModelId = "gemini-x", Elo = 1480, Games = 2, Wins = 0,
        Place1 = 0, Place2 = 0, Place3 = 2, Place4 = 0,
      });
      ctx.Matches.Append(MakeRecord("m1", 1, ctx.RunId));
      ctx.Matches.Append(MakeRecord("m2", 2, ctx.RunId));

      var analytics = Body(controller.GetAnalytics(scope: "current"));
      Assert.AreEqual(2, analytics.Summary.TotalMatches);
      Assert.AreEqual(8, analytics.Summary.TotalRounds);
      Assert.AreEqual(2, analytics.Summary.WildcardMatches);

      var gpt = analytics.Leaderboard.Single(r => r.ModelId == "gpt-x");
      Assert.AreEqual(1, gpt.Rank);
      Assert.AreEqual(1560.0, gpt.Elo, 1e-9);
      Assert.AreEqual(0.5, gpt.AgariRate, 1e-9); // 4 agari / 8 rounds
      Assert.AreEqual(0.5, gpt.RiichiRate, 1e-9);
      Assert.AreEqual(2, gpt.WildcardWins);

      // Pairwise H2H records GPT-X beating Gemini-X in both matches.
      var h2h = analytics.HeadToHead.Single(c => c.ModelA == "gpt-x" && c.ModelB == "gemini-x");
      Assert.AreEqual(2, h2h.SharedMatches);
      Assert.AreEqual(2, h2h.ABeatB);
      Assert.AreEqual(1.0, h2h.WinRateA, 1e-9);

      // Yaku breakdown distinguishes standard yaku and classical koyaku!
      Assert.IsTrue(analytics.YakuBreakdown.Any(y => y.Name == "Riichi" && y.Group == "1han" && !y.IsKoyaku));
      Assert.IsTrue(analytics.YakuBreakdown.Any(y => y.Name == "TsubameGaeshi" && y.Group == "koyaku" && y.IsKoyaku));
    }

    [TestMethod]
    public void TestMatchesPaginationFilterAndReplayLinksWithPerSeatPerspectives() {
      var cfg = MakeConfig();
      var controller = MakeController(cfg);
      for (int i = 1; i <= 5; i++) {
        ctx.Matches.Append(MakeRecord("m" + i, i, ctx.RunId));
      }

      var page1 = Body(controller.GetMatches(page: 1, pageSize: 2));
      Assert.AreEqual(5, page1.TotalCount);
      Assert.AreEqual(2, page1.Items.Count);
      Assert.AreEqual("m5", page1.Items[0].MatchId);

      var top = page1.Items[0];
      Assert.AreEqual(ReplayLink(cfg, "game-m5"), top.ReplayLink);
      Assert.AreEqual(4, top.SeatReplayLinks.Count);
      Assert.AreEqual(ReplayLink(cfg, "game-m5", 2), top.SeatReplayLinks[2]);
      Assert.AreEqual("/api/arena/matches/game-m5/replay.pb", top.ReplayDownloadUrl);
    }

    [TestMethod]
    public void TestMatchDetailAndReplayBinaryDownload() {
      var cfg = MakeConfig();
      var controller = MakeController(cfg);
      ctx.Matches.Append(MakeRecord("m1", 3, ctx.RunId));
      replayStore.SaveReplay("game-m1", new GameLogMsg { GameId = "game-m1", CreatedAtUnixMs = 999 });

      var detail = Body(controller.GetMatch("m1"));
      Assert.IsNotNull(detail);
      Assert.AreEqual("m1", detail.MatchId);
      Assert.AreEqual(777_003, detail.Seed);
      Assert.IsTrue(detail.HasWildcards);
      Assert.AreEqual(ReplayLink(cfg, "game-m1"), detail.ReplayLink);
      Assert.AreEqual(4, detail.SeatReplayLinks.Count);

      var fileRes = controller.DownloadReplayPb("game-m1") as FileContentResult;
      Assert.IsNotNull(fileRes);
      var parsed = GameLogMsg.Parser.ParseFrom(fileRes.FileContents);
      Assert.AreEqual("game-m1", parsed.GameId);
    }

    [TestMethod]
    public void TestPublicReasoningRespectsExposeReasoningPubliclyGate() {
      var cfg = MakeConfig();
      cfg.ExposeReasoningPublicly = false;
      var controller = MakeController(cfg);
      var record = MakeRecord("m-priv", 1, ctx.RunId);
      ctx.Matches.Append(record);
      reasoningStore.WriteMeta(new ReasoningMeta {
        GameId = record.GameId, Seat = 0, ModelId = "gpt-x", SystemPrompt = "SYS",
      });
      reasoningStore.AppendTurn(record.GameId, 0, new ReasoningTurn {
        TurnSeq = 1, EventIndex = 4, Rationale = "cut honor", Valid = true,
      });

      // Unauthenticated caller gets 401 when ExposeReasoningPublicly == false.
      var denied = controller.GetPublicReasoning("m-priv", 0);
      Assert.IsInstanceOfType(denied.Result, typeof(UnauthorizedObjectResult));

      // Enabling ExposeReasoningPublicly serves the full turn transcript.
      cfg.ExposeReasoningPublicly = true;
      var allowed = Body(controller.GetPublicReasoning("m-priv", 0));
      Assert.AreEqual("SYS", allowed.Meta.SystemPrompt);
      Assert.AreEqual(1, allowed.Turns.Count);
    }

    [TestMethod]
    public void TestReasoningTrackExposesOnlyRationaleFor3DReplayBubbles() {
      var cfg = MakeConfig();
      var controller = MakeController(cfg);
      var record = MakeRecord("m-2", 2, ctx.RunId);
      ctx.Matches.Append(record);
      reasoningStore.AppendTurn(record.GameId, 1, new ReasoningTurn {
        EventIndex = 3,
        Rationale = "public rationale",
        PromptDelta = "SECRET PROMPT",
        RawResponse = "SECRET RESPONSE",
        Reasoning = "SECRET THINKING",
      });

      var json = System.Text.Json.JsonSerializer.Serialize(
          Body(controller.GetReasoningTrack("m-2")));

      StringAssert.Contains(json, "public rationale");
      Assert.IsFalse(json.Contains("SECRET"), json);
    }
  }
}
