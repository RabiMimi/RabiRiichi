using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Arena.Config;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Server.Services;

namespace RabiRiichi.Arena.Controllers {
  /// <summary>
  /// Public REST surface serving Arena metadata, live status &amp; active tables,
  /// rich scoreboards, multi-dimensional analytics, match histories, 3D replay
  /// links + raw protobuf downloads, and reasoning transcripts.
  /// </summary>
  [ApiController]
  [Route("api/arena")]
  [EnableCors("PublicReadCors")]
  public sealed class PublicController(
      ArenaConfig config,
      RunManager runManager,
      ArenaService arenaService,
      ReasoningStore reasoningStore,
      ReplayStore replayStore = null) : ControllerBase {
    private readonly ArenaConfig config = config;
    private readonly RunManager runManager = runManager;
    private readonly ArenaService arenaService = arenaService;
    private readonly ReasoningStore reasoningStore = reasoningStore;
    private readonly ReplayStore replayStore = replayStore;

    [HttpGet("metadata")]
    public ActionResult<ArenaMetadataDto> GetMetadata() {
      var presetCounts = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
      foreach (var p in ArenaMetadata.TilePresets) {
        if (p.Id == "Custom") continue;
        presetCounts[p.Id] = ArenaMetadata.GetPresetTileCounts(p.Id);
      }
      return Ok(new ArenaMetadataDto {
        Providers = ArenaMetadata.Providers,
        BaselineVariants = ArenaMetadata.BaselineVariants,
        TilePresets = ArenaMetadata.TilePresets,
        PresetTileCounts = presetCounts,
        CanonicalTileCodes = ArenaMetadata.CanonicalTileCodes,
        Yakus = ArenaMetadata.Yakus,
        DefaultAllowedYakus = ArenaMetadata.DefaultAllowedYakus(),
        PolicyCategories = ArenaMetadata.PolicyCategories,
        ExposeReasoningPublicly = config.ExposeReasoningPublicly,
        ClientUrl = config.ClientUrl ?? "",
      });
    }

    [HttpGet("runs")]
    public ActionResult<IReadOnlyList<RunSummaryDto>> GetRuns() {
      var activeId = arenaService.ActiveRunId;
      var runs = runManager.ListRuns().Select(r => MapRunSummary(r, activeId)).ToList();
      return Ok(runs);
    }

    [HttpGet("status")]
    public ActionResult<NextMatchDto> GetStatus(string runId = null) => GetNext(runId);

    [HttpGet("next")]
    public ActionResult<NextMatchDto> GetNext(string runId = null) {
      string effectiveRunId = string.IsNullOrWhiteSpace(runId) || runId == "current" || runId == "all"
          ? (!string.IsNullOrEmpty(arenaService.ActiveRunId) ? arenaService.ActiveRunId : runManager.NewestRunId())
          : runId;

      ArenaStatus status;
      if (effectiveRunId != null && effectiveRunId == arenaService.ActiveRunId) {
        status = arenaService.GetStatus();
      } else {
        var ctx = runManager.GetRun(effectiveRunId);
        status = ArenaService.BuildStatusFromState(ctx?.RunState.Get(), ctx?.Config ?? config);
      }

      var preview = status.NextMatch;
      return Ok(new NextMatchDto {
        Status = status.Status.ToString(),
        RunId = status.RunId,
        Label = status.Label,
        ScheduleMode = status.ScheduleMode,
        CurrentSwissRound = status.CurrentSwissRound,
        TotalSwissRounds = status.TotalSwissRounds,
        CompletedTablesInRound = status.CompletedTablesInRound,
        TotalTablesInRound = status.TotalTablesInRound,
        TargetMatches = status.TargetMatches,
        QueuedCustomTables = status.QueuedCustomTables,
        CompletedMatches = status.CompletedMatches,
        MatchConcurrency = status.MatchConcurrency,
        LastError = status.LastError,
        ActiveMatches = status.ActiveMatches.Select(MapLiveMatch).ToList(),
        NextMatch = preview == null ? null : new NextMatchPairingDto {
          SwissRound = preview.SwissRound,
          Label = preview.Label,
          Padded = preview.Padded,
          Seats = preview.Seats.Select(s => new NextMatchSeatDto {
            Seat = s.Seat,
            ModelId = s.ModelId,
            DisplayName = s.DisplayName,
          }).ToList(),
        },
      });
    }

    [HttpGet("leaderboard")]
    public ActionResult<IReadOnlyList<RichLeaderboardRow>> GetLeaderboard(
        string scope = null, string runId = null) {
      string target = !string.IsNullOrWhiteSpace(scope) ? scope : runId;
      var analytics = new ArenaAnalyticsService(runManager, config)
          .Build(target, arenaService.ActiveRunId);
      return Ok(analytics.Leaderboard);
    }

    [HttpGet("analytics")]
    public ActionResult<ArenaAnalyticsPayload> GetAnalytics(
        string scope = null, string runId = null) {
      string target = !string.IsNullOrWhiteSpace(scope) ? scope : runId;
      var analytics = new ArenaAnalyticsService(runManager, config)
          .Build(target, arenaService.ActiveRunId);
      return Ok(analytics);
    }

    [HttpGet("matches")]
    public ActionResult<MatchListDto> GetMatches(
        string runId = null,
        int page = 1,
        int pageSize = 20,
        string modelId = null,
        string q = null) {
      page = Math.Max(1, page);
      pageSize = Math.Clamp(pageSize, 1, 200);

      if (string.Equals(runId, "all", StringComparison.OrdinalIgnoreCase)) {
        var allEntries = new List<MatchIndexEntry>();
        foreach (var r in runManager.GetAllRunsChronological()) {
          allEntries.AddRange(r.Matches.ListAllIndex());
        }
        IEnumerable<MatchIndexEntry> filtered = allEntries
            .OrderByDescending(e => e.FinishedAt, StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(modelId)) {
          string m = modelId.Trim();
          filtered = filtered.Where(e =>
              e.Players.Any(p => string.Equals(p.ModelId, m, StringComparison.OrdinalIgnoreCase)));
        }
        if (!string.IsNullOrWhiteSpace(q)) {
          string s = q.Trim();
          filtered = filtered.Where(e =>
              e.MatchId.Contains(s, StringComparison.OrdinalIgnoreCase) ||
              (e.BatchLabel?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
              (e.TileSetPreset?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
              e.Players.Any(p =>
                  p.DisplayName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                  p.ModelId.Contains(s, StringComparison.OrdinalIgnoreCase)));
        }
        var materialized = filtered.ToList();
        var slice = materialized.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new MatchListDto {
          Page = page,
          PageSize = pageSize,
          TotalCount = materialized.Count,
          Items = slice.Select(MapMatchListItem).ToList(),
        });
      }

      var ctx = ResolveRun(runId);
      if (ctx == null) {
        return Ok(new MatchListDto { Page = page, PageSize = pageSize });
      }
      var result = ctx.Matches.List(page, pageSize, modelId, q);
      return Ok(new MatchListDto {
        Page = result.Page,
        PageSize = result.PageSize,
        TotalCount = result.TotalCount,
        Items = result.Items.Select(MapMatchListItem).ToList(),
      });
    }

    [HttpGet("matches/{matchId}")]
    public ActionResult<MatchDetailDto> GetMatch(string matchId, string runId = null) {
      if (!MatchStore.IsValidId(matchId)) {
        return BadRequest();
      }
      var record = FindMatchRecord(matchId, runId);
      if (record == null) {
        return NotFound();
      }

      int pc = Math.Max(2, record.PlayerCount > 0 ? record.PlayerCount : record.Players.Count);
      var reasoningSeats = new List<int>();
      if (ReasoningStore.IsValidGameId(record.GameId)) {
        for (int s = 0; s < pc; s++) {
          if (reasoningStore.ReadMeta(record.GameId, s) != null) {
            reasoningSeats.Add(s);
          }
        }
      }

      bool hasReplay = replayStore != null &&
                       ReplayStore.IsValidGameId(record.GameId) &&
                       replayStore.GetReplay(record.GameId) != null;

      return Ok(new MatchDetailDto {
        MatchId = record.MatchId,
        GameId = record.GameId,
        RunId = record.RunId,
        SwissRound = record.SwissRound,
        BatchLabel = record.BatchLabel,
        StartedAt = record.StartedAt,
        FinishedAt = record.FinishedAt,
        DurationMs = record.DurationMs,
        Seed = record.Seed,
        PlayerCount = pc,
        TotalRound = record.TotalRound,
        TileSetPreset = record.TileSetPreset,
        HasWildcards = record.HasWildcards,
        WildcardCount = record.WildcardCount,
        RoundsPlayed = record.RoundsPlayed,
        ExhaustiveDraws = record.ExhaustiveDraws,
        AbortedDraws = record.AbortedDraws,
        Config = record.Config,
        ReplayLink = BuildReplayLink(record.GameId),
        SeatReplayLinks = Enumerable.Range(0, pc).Select(s => BuildReplayLink(record.GameId, s)).ToList(),
        ReplayDownloadUrl = $"/api/arena/matches/{Uri.EscapeDataString(record.GameId)}/replay.pb",
        HasReplayFile = hasReplay,
        ReasoningSeats = reasoningSeats,
        Rounds = record.Rounds ?? new List<MatchRoundSummary>(),
        Players = record.Players.Select(p => new MatchDetailPlayerDto {
          Seat = p.Seat,
          ModelId = p.ModelId,
          DisplayName = p.DisplayName,
          Provider = p.Provider,
          FinalPoints = p.FinalPoints,
          Placement = p.Placement,
          EloBefore = p.EloBefore,
          EloAfter = p.EloAfter,
          EloDelta = p.EloAfter - p.EloBefore,
          RoundsPlayed = p.RoundsPlayed,
          AgariCount = p.AgariCount,
          TsumoCount = p.TsumoCount,
          RonCount = p.RonCount,
          DealInCount = p.DealInCount,
          DealInPoints = p.DealInPoints,
          TotalWinPoints = p.TotalWinPoints,
          TotalWinHan = p.TotalWinHan,
          MaxWinPoints = p.MaxWinPoints,
          RiichiCount = p.RiichiCount,
          OpenHandCount = p.OpenHandCount,
          CallCount = p.CallCount,
          KanCount = p.KanCount,
          NukiCount = p.NukiCount,
          TenpaiAtDrawCount = p.TenpaiAtDrawCount,
          WildcardWinCount = p.WildcardWinCount,
          YakuCounts = p.YakuCounts ?? new Dictionary<string, int>(),
          DecisionsCount = p.DecisionsCount,
          AutoDecisionsCount = p.AutoDecisionsCount,
          ValidFirstTryCount = p.ValidFirstTryCount,
          RetryCount = p.RetryCount,
          PenaltyCount = p.PenaltyCount,
          PromptTokens = p.PromptTokens,
          CompletionTokens = p.CompletionTokens,
          ReasoningTokens = p.ReasoningTokens,
          TotalLatencyMs = p.TotalLatencyMs,
          MaxLatencyMs = p.MaxLatencyMs,
        }).ToList(),
      });
    }

    [HttpGet("matches/{gameId}/replay.pb")]
    public IActionResult DownloadReplayPb(string gameId) {
      if (replayStore == null || !ReplayStore.IsValidGameId(gameId)) {
        return BadRequest();
      }
      var replay = replayStore.GetReplay(gameId);
      if (replay == null) {
        return NotFound();
      }
      byte[] bytes = replay.ToByteArray();
      return File(bytes, "application/octet-stream", $"replay-{gameId}.pb");
    }

    [HttpGet("matches/{matchId}/reasoning/{seat:int}")]
    public ActionResult<AdminReasoningDto> GetPublicReasoning(
        string matchId, int seat, string runId = null) {
      bool adminOk = AdminAuthAttribute.IsRequestAuthorized(HttpContext);
      if (!config.ExposeReasoningPublicly && !adminOk) {
        return Unauthorized(new { error = "Reasoning transcripts require admin authentication." });
      }
      if (!MatchStore.IsValidId(matchId) || !ReasoningStore.IsValidSeat(seat)) {
        return BadRequest();
      }
      var record = FindMatchRecord(matchId, runId);
      var gameId = record?.GameId ?? matchId;
      if (!ReasoningStore.IsValidGameId(gameId)) {
        return BadRequest();
      }
      var meta = reasoningStore.ReadMeta(gameId, seat);
      if (meta == null) {
        return NotFound();
      }
      return Ok(new AdminReasoningDto {
        Meta = meta,
        Turns = reasoningStore.ReadTurns(gameId, seat),
      });
    }

    [HttpGet("matches/{matchId}/reasoning-track")]
    public ActionResult<ReasoningTrackDto> GetReasoningTrack(
        string matchId, string runId = null) {
      if (!MatchStore.IsValidId(matchId)) {
        return BadRequest();
      }
      var record = FindMatchRecord(matchId, runId);
      var gameId = record?.GameId ?? matchId;
      var nameBySeat = record?.Players.ToDictionary(p => p.Seat, p => p.DisplayName);
      var seats = nameBySeat?.Keys.OrderBy(s => s).ToList()
          ?? Enumerable.Range(0, config.GameRules?.PlayerCount ?? 4).ToList();

      var entries = ReasoningTrack.Build(reasoningStore, gameId, seats,
          seat => nameBySeat != null && nameBySeat.TryGetValue(seat, out var n) ? n : "");
      return Ok(new ReasoningTrackDto {
        GameId = gameId,
        Entries = entries.Select(e => new ReasoningTrackEntryDto {
          EventIndex = e.EventIndex,
          Seat = e.Seat,
          DisplayName = e.DisplayName,
          Rationale = e.Rationale,
          Valid = e.Valid,
        }).ToList(),
      });
    }

    public static IReadOnlyList<RichLeaderboardRow> BuildLeaderboard(
        ArenaConfig config, RatingStore ratingStore) {
      var modelsById = (config.Models ?? [])
          .Where(m => !string.IsNullOrEmpty(m.Id))
          .GroupBy(m => m.Id, StringComparer.Ordinal)
          .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

      return ratingStore.GetAll()
          .OrderByDescending(r => r.Elo)
          .ThenBy(r => r.ModelId, StringComparer.Ordinal)
          .Select((r, i) => {
            modelsById.TryGetValue(r.ModelId, out var mm);
            int total = r.Place1 + r.Place2 + r.Place3 + r.Place4;
            return new RichLeaderboardRow {
              Rank = i + 1,
              ModelId = r.ModelId,
              DisplayName = !string.IsNullOrEmpty(mm?.DisplayName) ? mm.DisplayName : r.ModelId,
              Provider = mm?.Provider ?? "",
              Variant = mm?.Variant ?? "",
              IsBaseline = mm?.IsBaseline ?? false,
              IsFrozen = mm is { IsBaseline: true } && mm.FrozenElo != null,
              Elo = r.Elo,
              Games = r.Games,
              Wins = r.Wins,
              WinRate = r.Games > 0 ? (double)r.Wins / r.Games : 0,
              Top2Rate = r.Games > 0 ? (double)(r.Place1 + r.Place2) / r.Games : 0,
              FourthAvoidRate = r.Games > 0 ? 1.0 - ((double)r.Place4 / r.Games) : 0,
              AvgPlacement = total > 0
                  ? (r.Place1 * 1.0 + r.Place2 * 2.0 + r.Place3 * 3.0 + r.Place4 * 4.0) / total
                  : 0,
              Place1 = r.Place1,
              Place2 = r.Place2,
              Place3 = r.Place3,
              Place4 = r.Place4,
              Penalties = r.Penalties,
            };
          }).ToList();
    }

    internal static RunSummaryDto MapRunSummary(RunSummary r, string activeId) => new() {
      RunId = r.RunId,
      Label = r.Label,
      ScheduleMode = r.ScheduleMode,
      CreatedAt = r.CreatedAt,
      UpdatedAt = r.UpdatedAt,
      Status = r.Status,
      SwissRounds = r.SwissRounds,
      CurrentSwissRound = r.CurrentSwissRound,
      TargetMatches = r.TargetMatches,
      QueuedCustomTables = r.QueuedCustomTables,
      CompletedMatches = r.CompletedMatches,
      ModelCount = r.ModelCount,
      PlayerCount = r.PlayerCount,
      TotalRound = r.TotalRound,
      TileSetPreset = r.TileSetPreset,
      HasWildcards = r.HasWildcards,
      Active = r.RunId == activeId,
    };

    internal static LiveMatchDto MapLiveMatch(LiveMatchProgress m) => new() {
      GameId = m.GameId,
      RoundLabel = m.RoundLabel,
      WallRemaining = m.WallRemaining,
      TurnCount = m.TurnCount,
      StartedAt = m.StartedAt,
      Seats = m.Seats.Select(s => new LiveSeatDto {
        Seat = s.Seat,
        ModelId = s.ModelId,
        DisplayName = s.DisplayName,
        Points = s.Points,
        InRiichi = s.InRiichi,
      }).ToList(),
    };

    private MatchListItemDto MapMatchListItem(MatchIndexEntry e) {
      int pc = Math.Max(2, e.PlayerCount > 0 ? e.PlayerCount : e.Players.Count);
      return new MatchListItemDto {
        MatchId = e.MatchId,
        GameId = e.GameId,
        RunId = e.RunId,
        SwissRound = e.SwissRound,
        BatchLabel = e.BatchLabel,
        FinishedAt = e.FinishedAt,
        DurationMs = e.DurationMs,
        PlayerCount = pc,
        TotalRound = e.TotalRound,
        TileSetPreset = e.TileSetPreset,
        HasWildcards = e.HasWildcards,
        RoundsPlayed = e.RoundsPlayed,
        ReplayLink = BuildReplayLink(e.GameId),
        SeatReplayLinks = Enumerable.Range(0, pc).Select(s => BuildReplayLink(e.GameId, s)).ToList(),
        ReplayDownloadUrl = $"/api/arena/matches/{Uri.EscapeDataString(e.GameId)}/replay.pb",
        Players = e.Players.Select(p => new MatchListPlayerDto {
          Seat = p.Seat,
          ModelId = p.ModelId,
          DisplayName = p.DisplayName,
          Provider = p.Provider,
          FinalPoints = p.FinalPoints,
          Placement = p.Placement,
          EloAfter = p.EloAfter,
          EloDelta = p.EloDelta,
          AgariCount = p.AgariCount,
          DealInCount = p.DealInCount,
          PenaltyCount = p.PenaltyCount,
        }).ToList(),
      };
    }

    private MatchRecord FindMatchRecord(string matchId, string runId) {
      if (!string.IsNullOrWhiteSpace(runId) && runId != "all" && runId != "current") {
        var explicitRun = runManager.GetRun(runId);
        if (explicitRun != null) {
          var hit = explicitRun.Matches.Get(matchId);
          if (hit != null) return hit;
        }
      }
      var defaultCtx = ResolveRun(null);
      if (defaultCtx != null) {
        var hit = defaultCtx.Matches.Get(matchId);
        if (hit != null) return hit;
      }
      foreach (var r in runManager.GetAllRunsChronological()) {
        var hit = r.Matches.Get(matchId);
        if (hit != null) return hit;
      }
      return null;
    }

    private RunContext ResolveRun(string runId) {
      string id = string.IsNullOrWhiteSpace(runId) || runId == "current"
          ? (!string.IsNullOrEmpty(arenaService.ActiveRunId) ? arenaService.ActiveRunId : runManager.NewestRunId())
          : runId;
      return runManager.GetRun(id);
    }

    public string BuildReplayLink(string gameId, int? seat = null) {
      var clientBase = (config.ClientUrl ?? "").TrimEnd('/');
      var wsBase = !string.IsNullOrWhiteSpace(config.WsUrl)
          ? config.WsUrl.TrimEnd('/')
          : DeriveWsBaseFromRequest();
      var url = $"{clientBase}?server={Uri.EscapeDataString(wsBase)}&replay={Uri.EscapeDataString(gameId ?? "")}&reasoning=1";
      if (seat.HasValue && seat.Value >= 0) {
        url += $"&seat={seat.Value}";
      }
      return url;
    }

    private string DeriveWsBaseFromRequest() {
      var request = HttpContext?.Request;
      if (request == null || !request.Host.HasValue) {
        return "";
      }
      var scheme = request.IsHttps ? "wss" : "ws";
      return $"{scheme}://{request.Host.Value}";
    }
  }
}
