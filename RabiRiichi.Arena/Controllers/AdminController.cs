using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Arena.Config;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Utils;

namespace RabiRiichi.Arena.Controllers {
  /// <summary>
  /// Admin REST controller providing session login/logout, full configuration
  /// updates, live model connection testing, comprehensive run/batch controls,
  /// and operational telemetry.
  /// </summary>
  [ApiController]
  [Route("api/admin")]
  [AdminAuth]
  public sealed class AdminController(
      ArenaConfigProvider configProvider,
      ArenaService arenaService,
      RunManager runManager,
      UsageStats usageStats,
      ReasoningStore reasoningStore,
      AdminSessionService sessionService = null,
      ILlmProviderFactory llmProviderFactory = null,
      IHttpClientFactory httpClientFactory = null) : ControllerBase {
    private readonly ArenaConfigProvider configProvider = configProvider;
    private readonly ArenaService arenaService = arenaService;
    private readonly RunManager runManager = runManager;
    private readonly UsageStats usageStats = usageStats;
    private readonly ReasoningStore reasoningStore = reasoningStore;
    private readonly AdminSessionService sessionService = sessionService;
    private readonly ILlmProviderFactory llmProviderFactory = llmProviderFactory;
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;

    // ----- Session auth (HttpOnly cookie + CSRF) --------------------------

    [HttpGet("session")]
    [AllowAnonymous]
    public ActionResult<AdminSessionDto> GetSession() {
      var configured = configProvider.Current.AdminPassword;
      bool defaultWarning = configured == "changeme";

      if (HttpContext != null &&
          Request.Headers.TryGetValue(AdminAuthAttribute.HeaderName, out var hdr) &&
          AdminAuthAttribute.IsAuthorized(configured, hdr.ToString())) {
        return Ok(new AdminSessionDto {
          Authenticated = true,
          CsrfToken = "",
          DefaultPasswordWarning = defaultWarning,
        });
      }

      if (sessionService != null &&
          HttpContext != null &&
          Request.Cookies.TryGetValue(AdminSessionService.CookieName, out var cookie) &&
          sessionService.TryValidateCookie(cookie, configured, out var csrf)) {
        return Ok(new AdminSessionDto {
          Authenticated = true,
          CsrfToken = csrf,
          DefaultPasswordWarning = defaultWarning,
        });
      }

      return Ok(new AdminSessionDto {
        Authenticated = false,
        CsrfToken = "",
        DefaultPasswordWarning = defaultWarning,
      });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public ActionResult<AdminSessionDto> Login([FromBody] AdminLoginRequestDto body) {
      var configured = configProvider.Current.AdminPassword;
      if (body == null || !AdminAuthAttribute.IsAuthorized(configured, body.Password)) {
        return Unauthorized(new { error = "Invalid admin password." });
      }

      string csrf = "";
      if (sessionService != null && HttpContext != null) {
        var (cookieVal, csrfToken, expiresAt) = sessionService.CreateSession(configured);
        csrf = csrfToken;
        Response.Cookies.Append(AdminSessionService.CookieName, cookieVal, new CookieOptions {
          HttpOnly = true,
          Secure = Request.IsHttps,
          SameSite = SameSiteMode.Strict,
          Expires = expiresAt,
          Path = "/",
        });
      }

      return Ok(new AdminSessionDto {
        Authenticated = true,
        CsrfToken = csrf,
        DefaultPasswordWarning = configured == "changeme",
      });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public ActionResult Logout() {
      if (HttpContext != null) {
        Response.Cookies.Delete(AdminSessionService.CookieName, new CookieOptions {
          HttpOnly = true,
          Secure = Request.IsHttps,
          SameSite = SameSiteMode.Strict,
          Path = "/",
        });
      }
      return Ok(new { ok = true });
    }

    // ----- Configuration Studio -------------------------------------------

    [HttpGet("config")]
    public ActionResult<ArenaConfig> GetConfig() {
      return Ok(configProvider.Current.Redacted());
    }

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
      Logger.Log("[Arena] Admin updated arena.config.json (hot-reloaded).");
      return Ok(new AdminConfigSaveResultDto {
        Saved = true,
        RestartRequiredFields = ArenaConfigProvider.RestartRequiredFields.ToList(),
      });
    }

    [HttpPost("models/test")]
    public async Task<ActionResult<ModelTestResultDto>> TestModel(
        [FromBody] ArenaConfig.ModelConfig incoming,
        CancellationToken cancellationToken) {
      if (incoming == null) {
        return BadRequest(new ModelTestResultDto {
          Ok = false,
          Message = "Model configuration body is required.",
        });
      }
      if (!string.IsNullOrWhiteSpace(incoming.BaseUrl) &&
          !ArenaConfig.IsSafeHttpOrRelativeUrl(incoming.BaseUrl, allowRelative: false)) {
        return BadRequest(new ModelTestResultDto {
          Ok = false,
          Message = "Base URL must use http:// or https://.",
        });
      }

      var probe = incoming.Clone();
      // Only reuse a stored API key when testing the exact same stored model ID,
      // provider, and base URL (prevents credential exfiltration to an unsaved custom URL).
      if (string.IsNullOrWhiteSpace(probe.ApiKey) && !string.IsNullOrWhiteSpace(probe.Id)) {
        var stored = configProvider.Current.Models
            .FirstOrDefault(m => string.Equals(m.Id, probe.Id, StringComparison.Ordinal));
        if (stored != null &&
            string.Equals(stored.Provider ?? "", probe.Provider ?? "", StringComparison.OrdinalIgnoreCase) &&
            string.Equals((stored.BaseUrl ?? "").TrimEnd('/'), (probe.BaseUrl ?? "").TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) {
          probe.ApiKey = stored.ApiKey;
        }
      }

      if (llmProviderFactory == null && !probe.IsBaseline) {
        return Ok(new ModelTestResultDto {
          Ok = false,
          Message = "LLM provider factory is not configured in this host.",
        });
      }

      var outcome = await ArenaProviderResolver.TestConnectionAsync(
          probe,
          timeoutSeconds: 25,
          http: httpClientFactory?.CreateClient("llm"),
          factory: llmProviderFactory,
          ct: cancellationToken);
      var msg = outcome.Ok
          ? $"Connected ({outcome.LatencyMs} ms) — chose [{outcome.ParsedAction}]" +
            (string.IsNullOrWhiteSpace(outcome.Rationale) ? "" : $": {outcome.Rationale}")
          : (outcome.Error ?? "Connection test failed.");
      return Ok(new ModelTestResultDto {
        Ok = outcome.Ok,
        Message = msg,
        LatencyMs = outcome.LatencyMs,
      });
    }

    // ----- Full Arena Run Controls ----------------------------------------

    [HttpPost("run/start")]
    public ActionResult<AdminStatusDto> StartRun() {
      arenaService.Resume();
      Logger.Log("[Arena] Admin started/resumed run.");
      return Ok(BuildStatus());
    }

    [HttpPost("run/resume")]
    public ActionResult<AdminStatusDto> ResumeRun() {
      arenaService.Resume();
      Logger.Log("[Arena] Admin resumed run.");
      return Ok(BuildStatus());
    }

    [HttpPost("run/pause")]
    public ActionResult<AdminStatusDto> PauseRun() {
      arenaService.Pause();
      Logger.Log("[Arena] Admin requested graceful pause.");
      return Ok(BuildStatus());
    }

    [HttpPost("run/new")]
    public async Task<ActionResult<AdminStatusDto>> NewRun([FromBody] StartRunRequestDto req = null) {
      StartRunOptions opts = req == null ? null : new StartRunOptions {
        Label = req.Label ?? "",
        ScheduleMode = req.ScheduleMode,
        SwissRounds = req.SwissRounds,
        TargetMatches = req.TargetMatches,
        MatchConcurrency = req.MatchConcurrency,
        CustomTables = req.CustomTables,
      };
      await arenaService.StartNewRunAsync(opts);
      Logger.Log("[Arena] Admin started a new run.");
      return Ok(BuildStatus());
    }

    [HttpPost("run/stop")]
    public async Task<ActionResult<AdminStatusDto>> StopRun() {
      await arenaService.StopAsync();
      Logger.Log("[Arena] Admin stopped run.");
      return Ok(BuildStatus());
    }

    [HttpPost("run/batch")]
    public ActionResult<AdminStatusDto> AppendBatch([FromBody] AppendBatchRequestDto req) {
      if (req == null) {
        return BadRequest(new { errors = new[] { "Batch request body is required." } });
      }
      var errors = arenaService.AppendBatch(new AppendBatchOptions {
        Mode = req.Mode,
        Count = req.Count,
        Label = req.Label,
        SeatModelIds = req.SeatModelIds ?? new List<string>(),
      });
      if (errors.Count > 0) {
        return BadRequest(new { errors });
      }
      Logger.Log($"[Arena] Admin appended batch (mode={req.Mode}, count={req.Count}).");
      return Ok(BuildStatus());
    }

    [HttpPost("runs/{runId}/activate")]
    public ActionResult<AdminStatusDto> ActivateRun(string runId) {
      if (!RunManager.IsValidRunId(runId)) {
        return BadRequest(new { error = "Invalid runId." });
      }
      if (!arenaService.ActivateRun(runId, out var error)) {
        return Conflict(new { error });
      }
      return Ok(BuildStatus());
    }

    [HttpDelete("runs/{runId}")]
    public ActionResult<AdminStatusDto> DeleteRun(string runId) {
      if (!RunManager.IsValidRunId(runId)) {
        return BadRequest(new { error = "Invalid runId." });
      }
      if (!arenaService.DeleteRun(runId, out var error)) {
        return Conflict(new { error });
      }
      Logger.Log($"[Arena] Admin deleted run {runId}.");
      return Ok(BuildStatus());
    }

    // ----- Monitoring & Reasoning -----------------------------------------

    [HttpGet("status")]
    public ActionResult<AdminStatusDto> GetStatus() {
      return Ok(BuildStatus());
    }

    [HttpPost("usage/reset")]
    public ActionResult<AdminStatusDto> ResetUsage([FromQuery] string modelId = null) {
      usageStats.Reset(modelId);
      return Ok(BuildStatus());
    }

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

    private AdminStatusDto BuildStatus() {
      var status = arenaService.GetStatus();
      var activeId = arenaService.ActiveRunId;
      var modelsById = configProvider.Current.Models
          .Where(m => !string.IsNullOrEmpty(m.Id))
          .GroupBy(m => m.Id, StringComparer.Ordinal)
          .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

      return new AdminStatusDto {
        Run = new AdminRunStateDto {
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
          ActiveMatches = status.ActiveMatches.Select(PublicController.MapLiveMatch).ToList(),
        },
        Runs = runManager.ListRuns().Select(r => PublicController.MapRunSummary(r, activeId)).ToList(),
        Models = usageStats.Snapshot().Select(u => ToUsageDto(u, modelsById)).ToList(),
      };
    }

    private static AdminModelUsageDto ToUsageDto(
        ModelUsage u, IReadOnlyDictionary<string, ArenaConfig.ModelConfig> modelsById) {
      modelsById.TryGetValue(u.ModelId, out var mc);
      long calls = u.Successes + u.Failures;
      return new AdminModelUsageDto {
        ModelId = u.ModelId,
        DisplayName = !string.IsNullOrEmpty(mc?.DisplayName) ? mc.DisplayName : u.ModelId,
        Provider = mc?.Provider ?? "",
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
        ReasoningTokens = u.ReasoningTokens,
        TotalTokens = u.TotalTokens,
        TotalLatencyMs = u.TotalLatencyMs,
        AvgLatencyMs = calls > 0 ? (double)u.TotalLatencyMs / calls : 0,
        Retries = u.Retries,
        Penalties = u.Penalties,
      };
    }
  }
}
