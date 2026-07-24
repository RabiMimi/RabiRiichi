using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Arena.Storage;

namespace RabiRiichi.Arena.Controllers {
  /// <summary>
  /// The public, no-auth REST surface for the Arena's own page (ARENA_DESIGN.md
  /// §12a, multi-run): the run list, and per-run leaderboard, next-match/status,
  /// paginated match history, and per-match detail. Every data endpoint is scoped
  /// to a run (<c>?runId=</c>, defaulting to the newest run). Thin: it reads the
  /// selected run's stores via <see cref="RunManager"/> + the live
  /// <see cref="ArenaService"/> status, and projects clean DTOs that never leak
  /// secrets or live-game seeds.
  /// </summary>
  [ApiController]
  [Route("api/arena")]
  public sealed class PublicController(
      ArenaConfig config,
      RunManager runManager,
      ArenaService arenaService) : ControllerBase {
    private readonly ArenaConfig config = config;
    private readonly RunManager runManager = runManager;
    private readonly ArenaService arenaService = arenaService;

    /// <summary>All runs (newest-first) for the run selector; marks the active one.</summary>
    [HttpGet("runs")]
    public ActionResult<IReadOnlyList<RunSummaryDto>> GetRuns() {
      var activeId = arenaService.ActiveRunId;
      var runs = runManager.ListRuns().Select(r => new RunSummaryDto {
        RunId = r.RunId,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
        Status = r.Status,
        SwissRounds = r.SwissRounds,
        CurrentSwissRound = r.CurrentSwissRound,
        CompletedMatches = r.CompletedMatches,
        ModelCount = r.ModelCount,
        Active = r.RunId == activeId,
      }).ToList();
      return Ok(runs);
    }

    /// <summary>Models ranked by Elo (desc) for the selected run. See §12a.</summary>
    [HttpGet("leaderboard")]
    public ActionResult<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboard(
        string runId = null) {
      var ctx = ResolveRun(runId);
      if (ctx == null) {
        return Ok(new List<LeaderboardEntryDto>());
      }
      return Ok(BuildLeaderboard(ctx.Config, ctx.Ratings));
    }

    /// <summary>
    /// Next pairing preview + run status for the selected run. For the active run
    /// this is the LIVE scheduler status (with cooldown countdown + preview); for
    /// any other run it is the persisted state. Never exposes the seed (§12a).
    /// </summary>
    [HttpGet("next")]
    public ActionResult<NextMatchDto> GetNext(string runId = null) {
      runId ??= runManager.NewestRunId();
      ArenaStatus status;
      if (runId != null && runId == arenaService.ActiveRunId) {
        status = arenaService.GetStatus();
      } else {
        var ctx = runManager.GetRun(runId);
        status = ArenaService.BuildStatusFromState(ctx?.RunState.Get(), ctx?.Config);
      }
      var preview = status.NextMatch;
      return Ok(new NextMatchDto {
        Status = status.Status.ToString(),
        RunId = status.RunId,
        CurrentSwissRound = status.CurrentSwissRound,
        TotalSwissRounds = status.TotalSwissRounds,
        CompletedTablesInRound = status.CompletedTablesInRound,
        TotalTablesInRound = status.TotalTablesInRound,
        CompletedMatches = status.CompletedMatches,
        SecondsToNextMatch = status.SecondsToNextMatch,
        NextMatch = preview == null ? null : new NextMatchPairingDto {
          SwissRound = preview.SwissRound,
          Padded = preview.Padded,
          Seats = preview.Seats.Select(s => new NextMatchSeatDto {
            Seat = s.Seat,
            ModelId = s.ModelId,
            DisplayName = s.DisplayName,
          }).ToList(),
        },
      });
    }

    /// <summary>Reverse-chronological, paginated match list for the selected run (§12a).</summary>
    [HttpGet("matches")]
    public ActionResult<MatchListDto> GetMatches(
        string runId = null, int page = 1, int pageSize = 20) {
      var ctx = ResolveRun(runId);
      if (ctx == null) {
        return Ok(new MatchListDto { Page = page, PageSize = pageSize });
      }
      var result = ctx.Matches.List(page, pageSize);
      return Ok(new MatchListDto {
        Page = result.Page,
        PageSize = result.PageSize,
        TotalCount = result.TotalCount,
        Items = result.Items.Select(e => new MatchListItemDto {
          MatchId = e.MatchId,
          GameId = e.GameId,
          FinishedAt = e.FinishedAt,
          ReplayLink = BuildReplayLink(e.GameId),
          Players = e.Players.Select(p => new MatchListPlayerDto {
            DisplayName = p.DisplayName,
            FinalPoints = p.FinalPoints,
            Placement = p.Placement,
            EloAfter = p.EloAfter,
            EloDelta = p.EloDelta,
          }).ToList(),
        }).ToList(),
      });
    }

    /// <summary>Full match detail incl. the per-game config + real seed (§12a).</summary>
    [HttpGet("matches/{matchId}")]
    public ActionResult<MatchDetailDto> GetMatch(string matchId, string runId = null) {
      var ctx = ResolveRun(runId);
      var record = ctx?.Matches.Get(matchId);
      if (record == null) {
        return NotFound();
      }
      return Ok(new MatchDetailDto {
        MatchId = record.MatchId,
        GameId = record.GameId,
        RunId = record.RunId,
        SwissRound = record.SwissRound,
        StartedAt = record.StartedAt,
        FinishedAt = record.FinishedAt,
        Seed = record.Seed,
        Config = record.Config,
        ReplayLink = BuildReplayLink(record.GameId),
        Players = record.Players.Select(p => new MatchDetailPlayerDto {
          Seat = p.Seat,
          ModelId = p.ModelId,
          DisplayName = p.DisplayName,
          FinalPoints = p.FinalPoints,
          Placement = p.Placement,
          PenaltyCount = p.PenaltyCount,
          EloBefore = p.EloBefore,
          EloAfter = p.EloAfter,
          EloDelta = p.EloAfter - p.EloBefore,
        }).ToList(),
      });
    }

    /// <summary>Resolves the requested run (defaulting to the newest), or null.</summary>
    private RunContext ResolveRun(string runId) =>
        runManager.GetRun(runId ?? runManager.NewestRunId());

    /// <summary>
    /// Builds a replay link (<c>{clientUrl}?server={wsBase}&amp;replay={gameId}</c>)
    /// where the client renders the replay by connecting to <c>{wsBase}/ws/public</c>.
    ///
    /// The Arena hosts <c>/ws/public</c> on its OWN origin (the same Kestrel port
    /// that served this request), so <c>wsBase</c> is derived from the incoming
    /// request by default — this is self-correcting regardless of the port the
    /// Arena actually runs on, and fixes stale links baked into old match records.
    /// A non-empty live <c>config.wsUrl</c> acts as an explicit override for
    /// reverse-proxy deployments where the WebSocket origin differs.
    /// </summary>
    private string BuildReplayLink(string gameId) {
      var clientBase = config.ClientUrl ?? "";
      var wsBase = !string.IsNullOrWhiteSpace(config.WsUrl)
          ? config.WsUrl
          : DeriveWsBaseFromRequest();
      return $"{clientBase}?server={wsBase}&replay={gameId}";
    }

    /// <summary>
    /// Derives the Arena's own WebSocket origin (<c>ws://host</c> / <c>wss://host</c>)
    /// from the current request, so replay links point back at whatever host:port
    /// the browser actually reached the Arena on. Returns an empty string when no
    /// request context is available (e.g. unit tests construct the controller
    /// directly, but those set <c>config.wsUrl</c> so this path is not hit).
    /// </summary>
    private string DeriveWsBaseFromRequest() {
      var request = HttpContext?.Request;
      if (request == null || !request.Host.HasValue) {
        return "";
      }
      var scheme = request.IsHttps ? "wss" : "ws";
      return $"{scheme}://{request.Host.Value}";
    }

    /// <summary>
    /// Ranks all rated models by Elo (desc), resolving display names from the
    /// run's config snapshot and computing average placement. Extracted (static +
    /// pure) so it is unit-testable without the ASP.NET pipeline.
    /// </summary>
    public static IReadOnlyList<LeaderboardEntryDto> BuildLeaderboard(
        ArenaConfig config, RatingStore ratingStore) {
      var nameById = config.Models
          .GroupBy(m => m.Id)
          .ToDictionary(g => g.Key, g => g.First().DisplayName);
      var entries = ratingStore.GetAll()
          .OrderByDescending(r => r.Elo)
          .ThenBy(r => r.ModelId)
          .Select((r, i) => new LeaderboardEntryDto {
            Rank = i + 1,
            ModelId = r.ModelId,
            DisplayName = nameById.TryGetValue(r.ModelId, out var n) && !string.IsNullOrEmpty(n)
                ? n : r.ModelId,
            Elo = r.Elo,
            Games = r.Games,
            Wins = r.Wins,
            AvgPlacement = AvgPlacement(r),
            Place1 = r.Place1,
            Place2 = r.Place2,
            Place3 = r.Place3,
            Place4 = r.Place4,
            Penalties = r.Penalties,
          })
          .ToList();
      return entries;
    }

    private static double AvgPlacement(RatingRecord r) {
      int total = r.Place1 + r.Place2 + r.Place3 + r.Place4;
      if (total == 0) {
        return 0;
      }
      return (r.Place1 * 1 + r.Place2 * 2 + r.Place3 * 3 + r.Place4 * 4) / (double)total;
    }
  }
}
