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
  /// Tests for the admin surface (ARENA_DESIGN.md §12b/§12c): auth gate
  /// (<see cref="AdminAuthAttribute"/>) and <see cref="AdminController"/>. These
  /// instantiate the controller/filter directly over temp-backed stores + config
  /// (no Kestrel), so nothing hits the network and no real games play.
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

    // ----- Fixtures --------------------------------------------------------

    private ArenaConfig MakeConfig(string adminPassword = "s3cr3t") {
      var cfg = new ArenaConfig {
        WorkspaceDir = workspaceDir,
        AdminPassword = adminPassword,
        ClientUrl = "https://play.example.com",
        WsUrl = "wss://arena.example.com",
        PublicUrl = "https://arena.example.com",
      };
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "gpt-x", DisplayName = "GPT-X", Provider = "openai", Model = "m",
        ApiKey = "sk-real-openai-key",
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "baseline-mid", DisplayName = "Rule (mid)", Provider = "baseline",
        FrozenElo = 1500,
      });
      return cfg;
    }

    // A fake table runner that never actually plays; used only where a run must
    // be startable/stoppable. Blocks until cancelled so Stop can be asserted.
    private static ArenaService.TableRunner BlockingRunner(TaskCompletionSource block) {
      return async (table, gameId, token) => {
        using var reg = token.Register(() => block.TrySetCanceled(token));
        await block.Task;
        token.ThrowIfCancellationRequested();
        return new EvalResult { GameId = gameId, Completed = true, Seats = new List<EvalSeatResult>() };
      };
    }

    private ArenaService MakeService(
        ArenaConfig cfg, ArenaService.TableRunner runner = null,
        ArenaService.DelayFn delay = null) {
      runManager ??= new RunManager(workspaceDir);
      return new ArenaService(
          runManager,
          currentConfig: () => cfg,
          tableRunner: runner ?? ((t, g, c) => Task.FromResult(
              new EvalResult { GameId = g, Completed = true, Seats = new List<EvalSeatResult>() })),
          delayFn: delay ?? ((span, token) => Task.CompletedTask));
    }

    private AdminController MakeController(
        ArenaConfig cfg, ArenaConfigProvider provider = null,
        ArenaService service = null, UsageStats usage = null) {
      provider ??= new ArenaConfigProvider(cfg, configPath);
      service ??= MakeService(cfg);
      runManager ??= new RunManager(workspaceDir);
      usage ??= new UsageStats(workspaceDir);
      return new AdminController(
          provider, service, runManager, usage, new ReasoningStore(workspaceDir));
    }

    private static T Body<T>(ActionResult<T> result) {
      if (result.Result is OkObjectResult ok) {
        return (T)ok.Value;
      }
      return result.Value;
    }

    // ----- Auth gate (§12b) ------------------------------------------------

    [TestMethod]
    public void TestAuth_CorrectPassword_Authorized() {
      Assert.IsTrue(AdminAuthAttribute.IsAuthorized("pw", "pw"));
    }

    [TestMethod]
    public void TestAuth_WrongPassword_Unauthorized() {
      Assert.IsFalse(AdminAuthAttribute.IsAuthorized("pw", "nope"));
    }

    [TestMethod]
    public void TestAuth_MissingPassword_Unauthorized() {
      Assert.IsFalse(AdminAuthAttribute.IsAuthorized("pw", ""));
      Assert.IsFalse(AdminAuthAttribute.IsAuthorized("pw", null));
    }

    [TestMethod]
    public void TestAuth_EmptyConfiguredPassword_FailsClosed() {
      // Fail closed: even a matching empty supplied value must be rejected.
      Assert.IsFalse(AdminAuthAttribute.IsAuthorized("", ""));
      Assert.IsFalse(AdminAuthAttribute.IsAuthorized("  ", "  "));
      Assert.IsFalse(AdminAuthAttribute.IsAuthorized(null, "anything"));
    }

    [TestMethod]
    public void TestAuthFilter_MissingHeader_SetsUnauthorized() {
      var cfg = MakeConfig("s3cr3t");
      var ctx = MakeAuthContext(cfg, suppliedHeader: null);
      new AdminAuthAttribute().OnAuthorization(ctx);
      Assert.IsInstanceOfType(ctx.Result, typeof(UnauthorizedResult));
    }

    [TestMethod]
    public void TestAuthFilter_CorrectHeader_Allows() {
      var cfg = MakeConfig("s3cr3t");
      var ctx = MakeAuthContext(cfg, suppliedHeader: "s3cr3t");
      new AdminAuthAttribute().OnAuthorization(ctx);
      Assert.IsNull(ctx.Result); // null => request proceeds
    }

    [TestMethod]
    public void TestAuthFilter_EmptyConfigured_FailsClosed() {
      var cfg = MakeConfig(adminPassword: "");
      var ctx = MakeAuthContext(cfg, suppliedHeader: "");
      new AdminAuthAttribute().OnAuthorization(ctx);
      Assert.IsInstanceOfType(ctx.Result, typeof(UnauthorizedResult));
    }

    /// <summary>Minimal single-entry service provider (avoids the DI package here).</summary>
    private sealed class StubProvider(ArenaConfigProvider provider) : IServiceProvider {
      public object GetService(Type serviceType) =>
          serviceType == typeof(ArenaConfigProvider) ? provider : null;
    }

    private Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext MakeAuthContext(
        ArenaConfig cfg, string suppliedHeader) {
      var httpCtx = new DefaultHttpContext {
        RequestServices = new StubProvider(new ArenaConfigProvider(cfg, configPath)),
      };
      if (suppliedHeader != null) {
        httpCtx.Request.Headers[AdminAuthAttribute.HeaderName] = suppliedHeader;
      }
      var actionCtx = new Microsoft.AspNetCore.Mvc.ActionContext(
          httpCtx,
          new Microsoft.AspNetCore.Routing.RouteData(),
          new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
      return new Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext(
          actionCtx, new List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>());
    }

    // ----- GET config is redacted (§12b) -----------------------------------

    [TestMethod]
    public void TestGetConfig_RedactsSecrets() {
      var cfg = MakeConfig("s3cr3t");
      var controller = MakeController(cfg);

      var got = Body(controller.GetConfig());
      Assert.IsNotNull(got);
      Assert.AreEqual("", got.AdminPassword, "adminPassword must be redacted.");
      foreach (var m in got.Models) {
        Assert.AreEqual("", m.ApiKey, $"apiKey for {m.Id} must be redacted.");
      }
      // Non-secret fields still present.
      Assert.AreEqual("https://play.example.com", got.ClientUrl);
      Assert.AreEqual(2, got.Models.Count);
    }

    // ----- PUT config: valid save + persist + secret preservation ----------

    [TestMethod]
    public void TestPutConfig_ValidSavesAndPersists_PreservesBlankSecrets() {
      var cfg = MakeConfig("s3cr3t");
      var provider = new ArenaConfigProvider(cfg, configPath);
      var controller = MakeController(cfg, provider);

      // Simulate the browser round-trip: GET (redacted) then edit a non-secret.
      var edited = cfg.Redacted();               // secrets blanked
      edited.ClientUrl = "https://new-client.example.com";

      var result = controller.PutConfig(edited);
      var dto = Body(result);
      Assert.IsTrue(dto.Saved);

      // In-memory config hot-reloaded (same singleton instance mutated).
      Assert.AreEqual("https://new-client.example.com", cfg.ClientUrl);
      // Secrets preserved (blank incoming kept the stored values).
      Assert.AreEqual("s3cr3t", cfg.AdminPassword);
      Assert.AreEqual("sk-real-openai-key",
          cfg.Models.Single(m => m.Id == "gpt-x").ApiKey);

      // Persisted to disk with the real secrets and new field.
      var onDisk = ArenaConfig.Load(configPath);
      Assert.AreEqual("https://new-client.example.com", onDisk.ClientUrl);
      Assert.AreEqual("s3cr3t", onDisk.AdminPassword);
      Assert.AreEqual("sk-real-openai-key",
          onDisk.Models.Single(m => m.Id == "gpt-x").ApiKey);
    }

    [TestMethod]
    public void TestPutConfig_OverwritesSecretWhenRealValueSupplied() {
      var cfg = MakeConfig("s3cr3t");
      var provider = new ArenaConfigProvider(cfg, configPath);
      var controller = MakeController(cfg, provider);

      var edited = cfg.Redacted();
      edited.AdminPassword = "new-admin-pw";      // real new secret
      edited.Models.Single(m => m.Id == "gpt-x").ApiKey = "sk-new-key";

      var dto = Body(controller.PutConfig(edited));
      Assert.IsTrue(dto.Saved);

      Assert.AreEqual("new-admin-pw", cfg.AdminPassword);
      Assert.AreEqual("sk-new-key",
          cfg.Models.Single(m => m.Id == "gpt-x").ApiKey);

      var onDisk = ArenaConfig.Load(configPath);
      Assert.AreEqual("new-admin-pw", onDisk.AdminPassword);
      Assert.AreEqual("sk-new-key",
          onDisk.Models.Single(m => m.Id == "gpt-x").ApiKey);
    }

    [TestMethod]
    public void TestPutConfig_Invalid_Returns400_DoesNotOverwriteFile() {
      var cfg = MakeConfig("s3cr3t");
      // Seed a valid file on disk first so we can prove it is not clobbered.
      cfg.Save(configPath);
      var provider = new ArenaConfigProvider(cfg, configPath);
      var controller = MakeController(cfg, provider);

      var bad = cfg.Redacted();
      bad.Run.TotalRound = 3;                     // invalid (must be 1 or 2)
      bad.WorkspaceDir = "";                      // invalid (empty)

      var result = controller.PutConfig(bad);
      Assert.IsInstanceOfType(result.Result, typeof(BadRequestObjectResult));
      var dto = (AdminConfigSaveResultDto)((BadRequestObjectResult)result.Result).Value;
      Assert.IsFalse(dto.Saved);
      Assert.IsTrue(dto.Errors.Count > 0);

      // The stored file is unchanged (still valid, real secret present).
      var onDisk = ArenaConfig.Load(configPath);
      Assert.AreEqual(2, onDisk.Run.TotalRound);
      Assert.AreEqual(workspaceDir, onDisk.WorkspaceDir);
      Assert.AreEqual("s3cr3t", onDisk.AdminPassword);
    }

    // ----- Run start/stop (§12b) -------------------------------------------

    [TestMethod]
    public async Task TestStartStop_TogglesArenaService() {
      var cfg = MakeConfig("s3cr3t");
      cfg.Run.SwissRounds = 5;
      cfg.Run.CooldownSecondsBetweenMatches = 0;
      cfg.Models.Clear();
      // Four baselines => a single startable table.
      for (int i = 0; i < 4; i++) {
        cfg.Models.Add(new ArenaConfig.ModelConfig {
          Id = "b" + i, DisplayName = "B" + i, Provider = "baseline",
          FrozenElo = 1500, Enabled = true,
        });
      }
      var block = new TaskCompletionSource();
      var service = MakeService(cfg, BlockingRunner(block));
      var controller = MakeController(cfg, service: service);

      Assert.AreEqual("Idle", Body(controller.GetStatus()).Run.Status);

      var started = Body(controller.StartRun());
      // The first match is held in-flight by the blocking runner.
      await WaitUntil(() => service.GetStatus().Status == RunStatus.Running);
      Assert.AreEqual("Running", Body(controller.GetStatus()).Run.Status);

      var stopped = Body(await controller.StopRun());
      Assert.AreEqual("Stopped", stopped.Run.Status);
      Assert.AreEqual("Stopped", Body(controller.GetStatus()).Run.Status);
    }

    [TestMethod]
    public async Task TestNewRun_CreatesSeparateRuns() {
      var cfg = MakeConfig("s3cr3t");
      cfg.Run.SwissRounds = 1;
      cfg.Run.CooldownSecondsBetweenMatches = 0;
      cfg.Models.Clear();
      for (int i = 0; i < 4; i++) {
        cfg.Models.Add(new ArenaConfig.ModelConfig {
          Id = "b" + i, DisplayName = "B" + i, Provider = "baseline",
          FrozenElo = 1500, Enabled = true,
        });
      }
      var service = MakeService(cfg); // instant runner
      var controller = MakeController(cfg, service: service);

      await controller.NewRun();
      await WaitUntil(() => service.GetStatus().Status == RunStatus.Finished);
      await controller.NewRun();
      await WaitUntil(() => service.GetStatus().Status == RunStatus.Finished);

      var status = Body(controller.GetStatus());
      Assert.AreEqual(2, status.Runs.Count, "each New run is a distinct run");
      Assert.AreEqual(1, status.Runs.Count(r => r.Active));
      Assert.AreEqual("Finished", status.Run.Status);
    }

    // ----- Status monitoring (§12c) ----------------------------------------

    [TestMethod]
    public void TestStatus_PerModelCounters_WithCategoryBreakdown_NoApiKeys() {
      var cfg = MakeConfig("s3cr3t");
      var usage = new UsageStats(workspaceDir);
      usage.RecordRequest("gpt-x");
      usage.RecordSuccess("gpt-x", promptTokens: 100, completionTokens: 30);
      usage.RecordRequest("gpt-x");
      usage.RecordFailure("gpt-x", UsageErrorCategory.Timeout);
      usage.RecordFailure("gpt-x", UsageErrorCategory.RateLimited);
      usage.RecordRetry("gpt-x", 2);
      usage.RecordPenalty("gpt-x");

      var controller = MakeController(cfg, usage: usage);
      var status = Body(controller.GetStatus());

      var m = status.Models.Single(x => x.ModelId == "gpt-x");
      Assert.AreEqual("GPT-X", m.DisplayName);
      Assert.AreEqual(2, m.Requests);
      Assert.AreEqual(1, m.Successes);
      Assert.AreEqual(2, m.Failures);
      Assert.AreEqual(1, m.TimeoutErrors);
      Assert.AreEqual(1, m.RateLimitedErrors);
      Assert.AreEqual(0, m.NetworkErrors);
      Assert.AreEqual(130, m.TotalTokens);
      Assert.AreEqual(100, m.PromptTokens);
      Assert.AreEqual(30, m.CompletionTokens);
      Assert.AreEqual(2, m.Retries);
      Assert.AreEqual(1, m.Penalties);

      // Serialize the whole DTO and prove no secret leaks.
      var json = System.Text.Json.JsonSerializer.Serialize(status);
      StringAssert.DoesNotMatch(json,
          new System.Text.RegularExpressions.Regex("apiKey", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
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
