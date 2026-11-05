using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Config;
using RabiRiichi.Arena.Controllers;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Arena.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Server.Arena {
  /// <summary>
  /// Tests for the admin surface: <see cref="AdminAuthAttribute"/>,
  /// <see cref="AdminSessionService"/> (HttpOnly cookie + CSRF + password rotation
  /// invalidation), and <see cref="AdminController"/> (config redaction/save,
  /// live model test, and full run/batch controls).
  /// </summary>
  [TestClass]
  public class AdminControllerTest {
    private string workspaceDir;
    private string configPath;
    private RunManager runManager;

    [TestInitialize]
    public void Setup() {
      workspaceDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_admin_{Guid.NewGuid():N}");
      Directory.CreateDirectory(workspaceDir);
      configPath = Path.Combine(workspaceDir, "arena.config.json");
    }

    [TestCleanup]
    public void Cleanup() {
      if (Directory.Exists(workspaceDir)) {
        try { Directory.Delete(workspaceDir, recursive: true); } catch { }
      }
    }

    private ArenaConfig MakeConfig(string adminPassword = "s3cr3t") {
      var cfg = new ArenaConfig {
        WorkspaceDir = workspaceDir,
        AdminPassword = adminPassword,
        ClientUrl = "https://play.example.com",
        WsUrl = "wss://arena.example.com",
      };
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "gpt-x", DisplayName = "GPT-X", Provider = "openai", Model = "m",
        BaseUrl = "https://api.openai.com",
        ApiKey = "sk-real-openai-key",
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "baseline-mid", DisplayName = "Rule (mid)", Provider = "baseline",
        Variant = "balanced", FrozenElo = 1500,
      });
      return cfg;
    }

    private static Func<SwissTable, string, CancellationToken, Task<EvalResult>> BlockingRunner(
        TaskCompletionSource block) {
      return async (table, gameId, token) => {
        using var reg = token.Register(() => block.TrySetCanceled(token));
        await block.Task;
        token.ThrowIfCancellationRequested();
        return new EvalResult { GameId = gameId, Completed = true, Seats = new List<EvalSeatResult>() };
      };
    }

    private ArenaService MakeService(
        ArenaConfig cfg,
        Func<SwissTable, string, CancellationToken, Task<EvalResult>> runner = null) {
      runManager ??= new RunManager(workspaceDir);
      return new ArenaService(
          runManager,
          currentConfig: () => cfg,
          simpleRunner: runner ?? ((t, g, c) => Task.FromResult(
              new EvalResult { GameId = g, Completed = true, Seats = new List<EvalSeatResult>() })));
    }

    private AdminController MakeController(
        ArenaConfig cfg, ArenaConfigProvider provider = null,
        ArenaService service = null, UsageStats usage = null,
        AdminSessionService sessions = null) {
      provider ??= new ArenaConfigProvider(cfg, configPath);
      service ??= MakeService(cfg);
      runManager ??= new RunManager(workspaceDir);
      usage ??= new UsageStats(workspaceDir);
      sessions ??= new AdminSessionService();
      return new AdminController(
          provider, service, runManager, usage, new ReasoningStore(workspaceDir), sessions);
    }

    private static T Body<T>(ActionResult<T> result) {
      if (result.Result is OkObjectResult ok) {
        return (T)ok.Value;
      }
      return result.Value;
    }

    // ----- Auth gate & HttpOnly Cookie + CSRF session ----------------------

    [TestMethod]
    public void TestAuth_PasswordHeaderValidation() {
      Assert.IsTrue(AdminAuthAttribute.IsAuthorized("pw", "pw"));
      Assert.IsFalse(AdminAuthAttribute.IsAuthorized("pw", "nope"));
      Assert.IsFalse(AdminAuthAttribute.IsAuthorized("pw", ""));
      Assert.IsFalse(AdminAuthAttribute.IsAuthorized("pw", null));
      Assert.IsFalse(AdminAuthAttribute.IsAuthorized("", ""));
      Assert.IsFalse(AdminAuthAttribute.IsAuthorized("  ", "  "));
      Assert.IsFalse(AdminAuthAttribute.IsAuthorized(null, "anything"));
    }

    [TestMethod]
    public void TestAuthFilter_HeaderAndSessionCookieWithCsrfAndPasswordRotation() {
      var cfg = MakeConfig("s3cr3t");
      var sessions = new AdminSessionService();
      var (cookieVal, csrf, _) = sessions.CreateSession("s3cr3t");

      // Missing header & cookie -> 401
      var unauthCtx = MakeAuthContext(cfg, sessions, method: "GET");
      new AdminAuthAttribute().OnAuthorization(unauthCtx);
      Assert.IsInstanceOfType(unauthCtx.Result, typeof(UnauthorizedResult));

      // Valid X-Admin-Password header -> allowed even on POST
      var hdrCtx = MakeAuthContext(cfg, sessions, method: "POST", suppliedHeader: "s3cr3t");
      new AdminAuthAttribute().OnAuthorization(hdrCtx);
      Assert.IsNull(hdrCtx.Result);

      // Valid session cookie on GET -> allowed without CSRF header
      var getCookieCtx = MakeAuthContext(cfg, sessions, method: "GET", cookie: cookieVal);
      new AdminAuthAttribute().OnAuthorization(getCookieCtx);
      Assert.IsNull(getCookieCtx.Result);

      // Valid session cookie on POST WITHOUT X-Arena-Csrf -> rejected (401)!
      var postNoCsrfCtx = MakeAuthContext(cfg, sessions, method: "POST", cookie: cookieVal);
      new AdminAuthAttribute().OnAuthorization(postNoCsrfCtx);
      Assert.IsInstanceOfType(postNoCsrfCtx.Result, typeof(UnauthorizedResult));

      // Valid session cookie on POST WITH matching X-Arena-Csrf -> allowed!
      var postCsrfCtx = MakeAuthContext(cfg, sessions, method: "POST", cookie: cookieVal, csrf: csrf);
      new AdminAuthAttribute().OnAuthorization(postCsrfCtx);
      Assert.IsNull(postCsrfCtx.Result);

      // Rotating AdminPassword immediately invalidates old session cookies!
      cfg.AdminPassword = "new-rotated-password";
      var rotatedCtx = MakeAuthContext(cfg, sessions, method: "GET", cookie: cookieVal);
      new AdminAuthAttribute().OnAuthorization(rotatedCtx);
      Assert.IsInstanceOfType(rotatedCtx.Result, typeof(UnauthorizedResult));
    }

    private sealed class StubProvider(ArenaConfigProvider provider, AdminSessionService sessions)
        : IServiceProvider {
      public object GetService(Type serviceType) {
        if (serviceType == typeof(ArenaConfigProvider)) return provider;
        if (serviceType == typeof(AdminSessionService)) return sessions;
        return null;
      }
    }

    private Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext MakeAuthContext(
        ArenaConfig cfg,
        AdminSessionService sessions,
        string method = "GET",
        string suppliedHeader = null,
        string cookie = null,
        string csrf = null) {
      var httpCtx = new DefaultHttpContext {
        RequestServices = new StubProvider(new ArenaConfigProvider(cfg, configPath), sessions),
      };
      httpCtx.Request.Method = method;
      if (suppliedHeader != null) {
        httpCtx.Request.Headers[AdminAuthAttribute.HeaderName] = suppliedHeader;
      }
      if (cookie != null) {
        httpCtx.Request.Headers.Cookie = $"{AdminSessionService.CookieName}={cookie}";
      }
      if (csrf != null) {
        httpCtx.Request.Headers[AdminSessionService.CsrfHeaderName] = csrf;
      }
      var actionCtx = new ActionContext(
          httpCtx,
          new Microsoft.AspNetCore.Routing.RouteData(),
          new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
      return new Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext(
          actionCtx, new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>());
    }

    // ----- Config Redaction & Save -----------------------------------------

    [TestMethod]
    public void TestGetConfig_RedactsSecretsAndSetsHasApiKeyFlag() {
      var cfg = MakeConfig("s3cr3t");
      var controller = MakeController(cfg);

      var got = Body(controller.GetConfig());
      Assert.IsNotNull(got);
      Assert.AreEqual("", got.AdminPassword);
      var gpt = got.Models.Single(m => m.Id == "gpt-x");
      Assert.IsNull(gpt.ApiKey);
      Assert.AreEqual(true, gpt.HasApiKey);

      var baseModel = got.Models.Single(m => m.Id == "baseline-mid");
      Assert.IsNull(baseModel.ApiKey);
      Assert.AreEqual(false, baseModel.HasApiKey);
    }

    [TestMethod]
    public void TestPutConfig_ValidSavesAndPersists_PreservesBlankSecrets() {
      var cfg = MakeConfig("s3cr3t");
      var provider = new ArenaConfigProvider(cfg, configPath);
      var controller = MakeController(cfg, provider);

      var edited = cfg.Redacted();
      edited.ClientUrl = "https://new-client.example.com";

      var dto = Body(controller.PutConfig(edited));
      Assert.IsTrue(dto.Saved);

      Assert.AreEqual("https://new-client.example.com", cfg.ClientUrl);
      Assert.AreEqual("s3cr3t", cfg.AdminPassword);
      Assert.AreEqual("sk-real-openai-key",
          cfg.Models.Single(m => m.Id == "gpt-x").ApiKey);

      var onDisk = ArenaConfig.Load(configPath);
      Assert.AreEqual("https://new-client.example.com", onDisk.ClientUrl);
      Assert.AreEqual("s3cr3t", onDisk.AdminPassword);
      Assert.AreEqual("sk-real-openai-key",
          onDisk.Models.Single(m => m.Id == "gpt-x").ApiKey);
    }

    [TestMethod]
    public void TestPutConfig_Invalid_Returns400_DoesNotOverwriteFile() {
      var cfg = MakeConfig("s3cr3t");
      cfg.Save(configPath);
      var provider = new ArenaConfigProvider(cfg, configPath);
      var controller = MakeController(cfg, provider);

      var bad = cfg.Redacted();
      bad.GameRules.TotalRound = 3; // invalid (must be 1 or 2)
      bad.WorkspaceDir = "";

      var result = controller.PutConfig(bad);
      Assert.IsInstanceOfType(result.Result, typeof(BadRequestObjectResult));
      var dto = (AdminConfigSaveResultDto)((BadRequestObjectResult)result.Result).Value;
      Assert.IsFalse(dto.Saved);
      Assert.IsTrue(dto.Errors.Count > 0);

      var onDisk = ArenaConfig.Load(configPath);
      Assert.AreEqual(1, onDisk.GameRules.TotalRound);
      Assert.AreEqual(workspaceDir, onDisk.WorkspaceDir);
      Assert.AreEqual("s3cr3t", onDisk.AdminPassword);
    }

    [TestMethod]
    public async Task TestModelEndpoint_BaselineSucceedsAndBlocksUnsafeBaseUrl() {
      var cfg = MakeConfig("s3cr3t");
      var controller = MakeController(cfg);

      var okRes = Body(await controller.TestModel(new ArenaConfig.ModelConfig {
        Id = "baseline-mid", Provider = "baseline", Variant = "balanced",
      }, CancellationToken.None));
      Assert.IsTrue(okRes.Ok);

      var unsafeUrl = await controller.TestModel(new ArenaConfig.ModelConfig {
        Id = "gpt-x", Provider = "openai", Model = "m", BaseUrl = "file:///etc/passwd",
      }, CancellationToken.None);
      Assert.IsInstanceOfType(unsafeUrl.Result, typeof(BadRequestObjectResult));
    }

    // ----- Run & Batch Controls --------------------------------------------

    [TestMethod]
    public async Task TestStartStopAndBatchControls() {
      var cfg = MakeConfig("s3cr3t");
      cfg.Run.SwissRounds = 5;
      cfg.Models.Clear();
      for (int i = 0; i < 4; i++) {
        cfg.Models.Add(new ArenaConfig.ModelConfig {
          Id = "b" + i, DisplayName = "B" + i, Provider = "baseline",
          Variant = "balanced", FrozenElo = 1500, Enabled = true,
        });
      }
      var block = new TaskCompletionSource();
      var service = MakeService(cfg, BlockingRunner(block));
      var controller = MakeController(cfg, service: service);

      Assert.AreEqual("Idle", Body(controller.GetStatus()).Run.Status);

      controller.StartRun();
      await WaitUntil(() => service.GetStatus().Status == RunStatus.Running);
      Assert.AreEqual("Running", Body(controller.GetStatus()).Run.Status);

      var stopped = Body(await controller.StopRun());
      Assert.AreEqual("Stopped", stopped.Run.Status);
    }

    [TestMethod]
    public async Task TestNewRunActivateAndDeleteRuns() {
      var cfg = MakeConfig("s3cr3t");
      cfg.Run.SwissRounds = 1;
      cfg.Models.Clear();
      for (int i = 0; i < 4; i++) {
        cfg.Models.Add(new ArenaConfig.ModelConfig {
          Id = "b" + i, DisplayName = "B" + i, Provider = "baseline",
          Variant = "balanced", FrozenElo = 1500, Enabled = true,
        });
      }
      var service = MakeService(cfg);
      var controller = MakeController(cfg, service: service);

      await controller.NewRun(new StartRunRequestDto { Label = "First Run" });
      await WaitUntil(() => service.GetStatus().Status == RunStatus.Finished);
      var run1Id = service.ActiveRunId;

      await controller.NewRun(new StartRunRequestDto { Label = "Second Run" });
      await WaitUntil(() => service.GetStatus().Status == RunStatus.Finished);
      var run2Id = service.ActiveRunId;

      var status = Body(controller.GetStatus());
      Assert.AreEqual(2, status.Runs.Count);

      var afterActivate = Body(controller.ActivateRun(run1Id));
      Assert.AreEqual(run1Id, afterActivate.Run.RunId);

      var afterDelete = Body(controller.DeleteRun(run2Id));
      Assert.AreEqual(1, afterDelete.Runs.Count);
    }

    [TestMethod]
    public void TestStatus_PerModelCounters_NoSecretLeaks() {
      var cfg = MakeConfig("s3cr3t");
      var usage = new UsageStats(workspaceDir);
      usage.RecordRequest("gpt-x");
      usage.RecordSuccess("gpt-x", promptTokens: 100, completionTokens: 30, reasoningTokens: 20, latencyMs: 450);
      usage.RecordRequest("gpt-x");
      usage.RecordFailure("gpt-x", UsageErrorCategory.Timeout, latencyMs: 150);
      usage.RecordRetry("gpt-x", 2);
      usage.RecordPenalty("gpt-x");

      var controller = MakeController(cfg, usage: usage);
      var status = Body(controller.GetStatus());

      var m = status.Models.Single(x => x.ModelId == "gpt-x");
      Assert.AreEqual("GPT-X", m.DisplayName);
      Assert.AreEqual(2, m.Requests);
      Assert.AreEqual(1, m.Successes);
      Assert.AreEqual(1, m.Failures);
      Assert.AreEqual(1, m.TimeoutErrors);
      Assert.AreEqual(130, m.TotalTokens);
      Assert.AreEqual(20, m.ReasoningTokens);
      Assert.AreEqual(300.0, m.AvgLatencyMs, 1e-6);

      var json = System.Text.Json.JsonSerializer.Serialize(status);
      Assert.IsFalse(json.Contains("sk-real-openai-key"));
      Assert.IsFalse(json.Contains("s3cr3t"));
    }

    private static async Task WaitUntil(Func<bool> cond, int timeoutMs = 5000) {
      var sw = Stopwatch.StartNew();
      while (!cond()) {
        if (sw.ElapsedMilliseconds > timeoutMs) {
          Assert.Fail("Condition not met within timeout.");
        }
        await Task.Delay(10);
      }
    }
  }
}
