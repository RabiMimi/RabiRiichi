using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Arena.Config;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Utils;

namespace RabiRiichi.Arena.Controllers {
  /// <summary>
  /// The password-gated admin REST surface (ARENA_DESIGN.md §12b). ALL endpoints
  /// require the admin password (see <see cref="AdminAuthAttribute"/>). Thin: it
  /// coordinates the <see cref="ArenaConfigProvider"/> (config editor), the
  /// <see cref="ArenaService"/> (run start/stop + state), and <see cref="UsageStats"/>
  /// (per-model monitoring). Secrets are NEVER returned: config is redacted on
  /// GET and status carries only usage counters.
  /// </summary>
  [ApiController]
  [Route("api/admin")]
  [AdminAuth]
  public sealed class AdminController(
      ArenaConfigProvider configProvider,
      ArenaService arenaService,
      RunManager runManager,
      UsageStats usageStats,
      ReasoningStore reasoningStore) : ControllerBase {
    private readonly ArenaConfigProvider configProvider = configProvider;
    private readonly ArenaService arenaService = arenaService;
    private readonly RunManager runManager = runManager;
    private readonly UsageStats usageStats = usageStats;
    private readonly ReasoningStore reasoningStore = reasoningStore;

    // ----- Config editor (§12b/§13) ---------------------------------------

    /// <summary>
    /// Returns the FULL config with every secret redacted (via
    /// <c>ArenaConfig.Redacted()</c>) so apiKeys/adminPassword never reach the
    /// browser.
    /// </summary>
    [HttpGet("config")]
    public ActionResult<ArenaConfig> GetConfig() {
      return Ok(configProvider.Current.Redacted());
    }

    /// <summary>
    /// Overwrites <c>arena.config.json</c> with the posted config. Because GET
    /// returns redacted secrets, any secret left blank/placeholder in the body is
    /// KEPT from the stored config; only a real new value overwrites a secret
    /// (§12b). Validates before saving; returns 400 with the errors list (and does
    /// NOT touch the file) when invalid. On success, hot-reloads the in-memory
    /// config; some fields need a restart (reported in the response).
    /// </summary>
    [HttpPut("config")]
    public ActionResult<AdminConfigSaveResultDto> PutConfig([FromBody] ArenaConfig incoming) {
      if (incoming == null) {
        return BadRequest(new AdminConfigSaveResultDto {
          Saved = false,
          Errors = { "Request body was empty or malformed." },
        });
      }
      var result = configProvider.Update(incoming);
      if (!result.Success) {
        return BadRequest(new AdminConfigSaveResultDto {
          Saved = false,
          Errors = result.Errors.ToList(),
        });
      }
      Logger.Log("[Arena] Admin overwrote arena.config.json (hot-reloaded).");
      return Ok(new AdminConfigSaveResultDto {
        Saved = true,
        RestartRequiredFields = ArenaConfigProvider.RestartRequiredFields.ToList(),
      });
    }

    // ----- Run control (§12b) ---------------------------------------------

    /// <summary>Starts, or RESUMES the newest unfinished, run; returns the status.</summary>
    [HttpPost("run/start")]
    public ActionResult<AdminStatusDto> StartRun() {
      arenaService.Start();
      Logger.Log("[Arena] Admin started/resumed the run.");
      return Ok(BuildStatus());
    }

    /// <summary>
    /// Starts a brand-new run, freezing the current config into it (past runs are
    /// left untouched). Stops any active run first. Returns the new status.
    /// </summary>
    [HttpPost("run/new")]
    public async Task<ActionResult<AdminStatusDto>> NewRun() {
      await arenaService.StartNewRunAsync();
      Logger.Log("[Arena] Admin started a NEW run (config snapshotted).");
      return Ok(BuildStatus());
    }

    /// <summary>Stops the run (cancels the in-flight match); returns the status.</summary>
    [HttpPost("run/stop")]
    public async Task<ActionResult<AdminStatusDto>> StopRun() {
      await arenaService.StopAsync();
      Logger.Log("[Arena] Admin stopped the run.");
      return Ok(BuildStatus());
    }

    // ----- Monitoring (§12c) ----------------------------------------------

    /// <summary>
    /// Per-model monitoring (requests/successes/failures-by-category/tokens/
    /// retries/penalties) plus current run/match state. NEVER includes apiKeys.
    /// </summary>
    [HttpGet("status")]
    public ActionResult<AdminStatusDto> GetStatus() {
      return Ok(BuildStatus());
    }

    // ----- Reasoning inspection (§8/§12b, optional) ------------------------

    /// <summary>
    /// Returns the reasoning transcript (meta + append-only turns) for a match
    /// seat, for admin inspection. 404 when no meta exists for that (gameId,seat).
    /// </summary>
    [HttpGet("matches/{gameId}/reasoning/{seat:int}")]
    public ActionResult<AdminReasoningDto> GetReasoning(string gameId, int seat) {
      if (!ReasoningStore.IsValidGameId(gameId) || !ReasoningStore.IsValidSeat(seat)) {
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

    // ----- Projection helpers ---------------------------------------------

    private AdminStatusDto BuildStatus() {
      var status = arenaService.GetStatus();
      var activeId = arenaService.ActiveRunId;
      var nameById = configProvider.Current.Models
          .Where(m => !string.IsNullOrEmpty(m.Id))
          .GroupBy(m => m.Id)
          .ToDictionary(g => g.Key, g => g.First().DisplayName);
      return new AdminStatusDto {
        Run = new AdminRunStateDto {
          Status = status.Status.ToString(),
          RunId = status.RunId,
          CurrentSwissRound = status.CurrentSwissRound,
          TotalSwissRounds = status.TotalSwissRounds,
          CompletedTablesInRound = status.CompletedTablesInRound,
          TotalTablesInRound = status.TotalTablesInRound,
          CompletedMatches = status.CompletedMatches,
          SecondsToNextMatch = status.SecondsToNextMatch,
        },
        Runs = runManager.ListRuns().Select(r => new RunSummaryDto {
          RunId = r.RunId,
          CreatedAt = r.CreatedAt,
          UpdatedAt = r.UpdatedAt,
          Status = r.Status,
          SwissRounds = r.SwissRounds,
          CurrentSwissRound = r.CurrentSwissRound,
          CompletedMatches = r.CompletedMatches,
          ModelCount = r.ModelCount,
          Active = r.RunId == activeId,
        }).ToList(),
        Models = usageStats.Snapshot().Select(u => ToUsageDto(u, nameById)).ToList(),
      };
    }

    private static AdminModelUsageDto ToUsageDto(
        ModelUsage u, IReadOnlyDictionary<string, string> nameById) {
      nameById.TryGetValue(u.ModelId, out var name);
      return new AdminModelUsageDto {
        ModelId = u.ModelId,
        DisplayName = string.IsNullOrEmpty(name) ? u.ModelId : name,
        Requests = u.Requests,
        Successes = u.Successes,
        Failures = u.Failures,
        NetworkErrors = u.NetworkErrors,
        TimeoutErrors = u.TimeoutErrors,
        InvalidResponseErrors = u.InvalidResponseErrors,
        RateLimitedErrors = u.RateLimitedErrors,
        AuthErrors = u.AuthErrors,
        OtherErrors = u.OtherErrors,
        PromptTokens = u.PromptTokens,
        CompletionTokens = u.CompletionTokens,
        TotalTokens = u.TotalTokens,
        Retries = u.Retries,
        Penalties = u.Penalties,
      };
    }
  }
}
