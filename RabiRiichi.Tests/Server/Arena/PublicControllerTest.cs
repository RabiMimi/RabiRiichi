using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Controllers;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Arena.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace RabiRiichi.Tests.Server.Arena {
  /// <summary>
  /// Tests for the public REST controller (ARENA_DESIGN.md §12a). These
  /// instantiate <see cref="PublicController"/> directly over temp-backed stores
  /// (no Kestrel): leaderboard ordering, matches pagination shape, match detail
  /// (seed + config), and the replay-link format.
  /// </summary>
  [TestClass]
  public class PublicControllerTest {
    private string workspaceDir;
    private RunManager runManager;
    private RunContext ctx;

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

    // ----- Fixtures --------------------------------------------------------

    private ArenaConfig MakeConfig() {
      var cfg = new ArenaConfig {
        WorkspaceDir = workspaceDir,
        AdminPassword = "pw",
        ClientUrl = "https://play.example.com",
        WsUrl = "wss://arena.example.com",
        PublicUrl = "https://arena.example.com",
      };
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "gpt-x", DisplayName = "GPT-X", Provider = "openai", Model = "m",
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "gemini-x", DisplayName = "Gemini-X", Provider = "gemini", Model = "m",
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "baseline-mid", DisplayName = "Rule (mid)", Provider = "baseline",
        FrozenElo = 1500,
      });
      return cfg;
    }

    // Creates a single run (the newest, so it is the default the controller reads)
    // and returns a controller over it. Populate ctx.Ratings / ctx.Matches after.
    private PublicController MakeController(ArenaConfig cfg) {
      runManager = new RunManager(workspaceDir);
      ctx = runManager.CreateRun(cfg);
      var arenaService = new ArenaService(runManager, () => cfg);
      return new PublicController(cfg, runManager, arenaService);
    }

    private static string ReplayLink(ArenaConfig cfg, string gameId) =>
        $"{cfg.ClientUrl}?server={cfg.WsUrl}&replay={gameId}";

    private static MatchRecord MakeRecord(ArenaConfig cfg, string matchId, int order) {
      var gameId = "game-" + matchId;
      return new MatchRecord {
        MatchId = matchId,
        GameId = gameId,
        RunId = "run-1",
        SwissRound = 1,
        StartedAt = "2024-01-01T00:00:00Z",
        FinishedAt = $"2024-01-01T00:0{order}:00Z",
        Seed = 777_000 + order,
        Config = new JsonObject { ["playerCount"] = 4, ["totalRound"] = 2, ["seed"] = 777_000 + order },
        Players = new List<MatchPlayer> {
          new() { Seat = 0, ModelId = "gpt-x", DisplayName = "GPT-X",
                  FinalPoints = 40000, Placement = 1, EloBefore = 1500, EloAfter = 1512 },
          new() { Seat = 1, ModelId = "gemini-x", DisplayName = "Gemini-X",
                  FinalPoints = 20000, Placement = 3, EloBefore = 1500, EloAfter = 1494 },
        },
        ReplayLink = ReplayLink(cfg, gameId),
      };
    }

    private static T Body<T>(ActionResult<T> result) {
      // OkObjectResult wraps the DTO; unwrap either an explicit Ok(...) or value.
      if (result.Result is OkObjectResult ok) {
        return (T)ok.Value;
      }
      return result.Value;
    }

    // ----- Leaderboard -----------------------------------------------------

    [TestMethod]
    public void TestLeaderboardOrderedByEloDesc() {
      var cfg = MakeConfig();
      var controller = MakeController(cfg);
      ctx.Ratings.Update(new RatingRecord {
        ModelId = "gpt-x", Elo = 1560, Games = 4, Wins = 2,
        Place1 = 2, Place2 = 1, Place3 = 1, Place4 = 0, Penalties = 1,
      });
      ctx.Ratings.Update(new RatingRecord {
        ModelId = "gemini-x", Elo = 1480, Games = 4, Wins = 1,
        Place1 = 1, Place2 = 1, Place3 = 1, Place4 = 1,
      });
      ctx.Ratings.Update(new RatingRecord {
        ModelId = "baseline-mid", Elo = 1500, Games = 4,
        Place1 = 1, Place2 = 2, Place3 = 1, Place4 = 0,
      });

      var board = Body(controller.GetLeaderboard());
      Assert.AreEqual(3, board.Count);
      // Descending Elo: gpt-x (1560) > baseline-mid (1500) > gemini-x (1480).
      Assert.AreEqual("gpt-x", board[0].ModelId);
      Assert.AreEqual(1, board[0].Rank);
      Assert.AreEqual("baseline-mid", board[1].ModelId);
      Assert.AreEqual(2, board[1].Rank);
      Assert.AreEqual("gemini-x", board[2].ModelId);
      Assert.AreEqual(3, board[2].Rank);

      // Display name resolved from config; counts + avg placement projected.
      Assert.AreEqual("GPT-X", board[0].DisplayName);
      Assert.AreEqual(1, board[0].Penalties);
      // avg = (2*1 + 1*2 + 1*3 + 0*4)/4 = 7/4 = 1.75
      Assert.AreEqual(1.75, board[0].AvgPlacement, 1e-9);
    }

    [TestMethod]
    public void TestLeaderboardFallsBackToModelIdWhenNoDisplayName() {
      var cfg = MakeConfig();
      var controller = MakeController(cfg);
      ctx.Ratings.Update(new RatingRecord { ModelId = "unknown-model", Elo = 1500 });

      var board = Body(controller.GetLeaderboard());
      Assert.AreEqual("unknown-model", board.Single().DisplayName);
    }

    // ----- Matches list ----------------------------------------------------

    [TestMethod]
    public void TestMatchesPaginationShapeNewestFirst() {
      var cfg = MakeConfig();
      var controller = MakeController(cfg);
      for (int i = 1; i <= 5; i++) {
        ctx.Matches.Append(MakeRecord(cfg, "m" + i, i));
      }

      var page1 = Body(controller.GetMatches(page: 1, pageSize: 2));
      Assert.AreEqual(1, page1.Page);
      Assert.AreEqual(2, page1.PageSize);
      Assert.AreEqual(5, page1.TotalCount);
      Assert.AreEqual(2, page1.Items.Count);
      Assert.AreEqual("m5", page1.Items[0].MatchId);
      Assert.AreEqual("m4", page1.Items[1].MatchId);

      // Item shape: players carry name/points/placement/elo delta + replay link.
      var top = page1.Items[0];
      Assert.AreEqual("game-m5", top.GameId);
      Assert.AreEqual(ReplayLink(cfg, "game-m5"), top.ReplayLink);
      var winner = top.Players.Single(p => p.DisplayName == "GPT-X");
      Assert.AreEqual(1, winner.Placement);
      Assert.AreEqual(12, winner.EloDelta);
      Assert.AreEqual(1512, winner.EloAfter);
    }

    // ----- Match detail ----------------------------------------------------

    [TestMethod]
    public void TestMatchDetailIncludesSeedAndConfig() {
      var cfg = MakeConfig();
      var controller = MakeController(cfg);
      ctx.Matches.Append(MakeRecord(cfg, "m1", 3));

      var detail = Body(controller.GetMatch("m1"));
      Assert.IsNotNull(detail);
      Assert.AreEqual("m1", detail.MatchId);
      Assert.AreEqual(777_003, detail.Seed);
      Assert.IsNotNull(detail.Config);
      Assert.AreEqual(4, (int)detail.Config["playerCount"]);
      Assert.AreEqual(777_003, (int)detail.Config["seed"]);
      Assert.AreEqual(ReplayLink(cfg, "game-m1"), detail.ReplayLink);

      var p0 = detail.Players.Single(p => p.Seat == 0);
      Assert.AreEqual("GPT-X", p0.DisplayName);
      Assert.AreEqual(12, p0.EloDelta);
    }

    [TestMethod]
    public void TestMatchDetailNotFound() {
      var cfg = MakeConfig();
      var controller = MakeController(cfg);
      var result = controller.GetMatch("does-not-exist");
      Assert.IsInstanceOfType(result.Result, typeof(NotFoundResult));
    }

    // ----- Next match / status --------------------------------------------

    [TestMethod]
    public void TestNextReportsIdleStatusWithoutSeed() {
      var cfg = MakeConfig();
      var controller = MakeController(cfg);

      var next = Body(controller.GetNext());
      Assert.AreEqual("Idle", next.Status);
      Assert.IsNull(next.NextMatch); // nothing scheduled
      Assert.AreEqual(cfg.Run.SwissRounds, next.TotalSwissRounds);
      // The DTO type has no seed field at all — cannot leak a live-game seed.
    }

    // ----- Runs list -------------------------------------------------------

    [TestMethod]
    public void TestRunsListsCreatedRuns() {
      var cfg = MakeConfig();
      var controller = MakeController(cfg); // creates one run
      var extra = runManager.CreateRun(cfg); // a second, newer run

      var runs = Body(controller.GetRuns());
      Assert.AreEqual(2, runs.Count);
      // Newest-first: the second-created run leads.
      Assert.AreEqual(extra.RunId, runs[0].RunId);
      Assert.AreEqual(ctx.RunId, runs[1].RunId);
    }

    [TestMethod]
    public void TestLeaderboardIsPerRun() {
      var cfg = MakeConfig();
      var controller = MakeController(cfg);
      ctx.Ratings.Update(new RatingRecord { ModelId = "gpt-x", Elo = 1600 });
      // A different, newer run has its own (empty) ratings.
      var other = runManager.CreateRun(cfg);

      // Default (newest) run has no ratings; the original run does.
      Assert.AreEqual(0, Body(controller.GetLeaderboard()).Count);
      Assert.AreEqual(1, Body(controller.GetLeaderboard(ctx.RunId)).Count);
      _ = other;
    }

    // ----- Replay link builder --------------------------------------------

    [TestMethod]
    public void TestReplayLinkFormat() {
      var cfg = MakeConfig();
      // The format the public page + records use: {clientUrl}?server={wsUrl}&replay={gameId}
      var link = ReplayLink(cfg, "abc-123");
      Assert.AreEqual(
          "https://play.example.com?server=wss://arena.example.com&replay=abc-123",
          link);
    }
  }
}
