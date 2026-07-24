using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Storage;
using System;
using System.IO;

namespace RabiRiichi.Tests.Server.Arena {
  [TestClass]
  public class UsageStatsTest {
    private string workspaceDir;

    [TestInitialize]
    public void Setup() {
      workspaceDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_stats_{Guid.NewGuid():N}");
      Directory.CreateDirectory(workspaceDir);
    }

    [TestCleanup]
    public void Cleanup() {
      if (Directory.Exists(workspaceDir)) {
        try {
          Directory.Delete(workspaceDir, recursive: true);
        } catch {
          // ignore
        }
      }
    }

    [TestMethod]
    public void TestIncrementsAndTokens() {
      var stats = new UsageStats(workspaceDir);
      stats.RecordRequest("gpt-x");
      stats.RecordRequest("gpt-x");
      stats.RecordSuccess("gpt-x", promptTokens: 100, completionTokens: 40);
      stats.RecordSuccess("gpt-x", promptTokens: 50, completionTokens: 10);
      stats.RecordRetry("gpt-x");
      stats.RecordPenalty("gpt-x");

      var u = stats.Get("gpt-x");
      Assert.IsNotNull(u);
      Assert.AreEqual(2, u.Requests);
      Assert.AreEqual(2, u.Successes);
      Assert.AreEqual(150, u.PromptTokens);
      Assert.AreEqual(50, u.CompletionTokens);
      Assert.AreEqual(200, u.TotalTokens);
      Assert.AreEqual(1, u.Retries);
      Assert.AreEqual(1, u.Penalties);
    }

    [TestMethod]
    public void TestErrorCategoryCounting() {
      var stats = new UsageStats(workspaceDir);
      stats.RecordFailure("gpt-x", UsageErrorCategory.Network);
      stats.RecordFailure("gpt-x", UsageErrorCategory.Network);
      stats.RecordFailure("gpt-x", UsageErrorCategory.Timeout);
      stats.RecordFailure("gpt-x", UsageErrorCategory.InvalidResponse);
      stats.RecordFailure("gpt-x", UsageErrorCategory.RateLimited);
      stats.RecordFailure("gpt-x", UsageErrorCategory.Auth);
      stats.RecordFailure("gpt-x", UsageErrorCategory.Other);

      var u = stats.Get("gpt-x");
      Assert.AreEqual(7, u.Failures);
      Assert.AreEqual(2, u.NetworkErrors);
      Assert.AreEqual(1, u.TimeoutErrors);
      Assert.AreEqual(1, u.InvalidResponseErrors);
      Assert.AreEqual(1, u.RateLimitedErrors);
      Assert.AreEqual(1, u.AuthErrors);
      Assert.AreEqual(1, u.OtherErrors);
    }

    [TestMethod]
    public void TestPerModelIsolationAndSnapshot() {
      var stats = new UsageStats(workspaceDir);
      stats.RecordRequest("gpt-x");
      stats.RecordRequest("gemini-x");
      stats.RecordSuccess("gemini-x");

      var snap = stats.Snapshot();
      Assert.AreEqual(2, snap.Count);
      // Snapshot is a copy; mutating it must not affect the store.
      snap[0].Requests = 999;
      Assert.AreNotEqual(999, stats.Get(snap[0].ModelId).Requests);
    }

    [TestMethod]
    public void TestPersistenceRoundTrip() {
      var stats = new UsageStats(workspaceDir);
      stats.RecordRequest("gpt-x");
      stats.RecordSuccess("gpt-x", 10, 5);
      stats.RecordFailure("gpt-x", UsageErrorCategory.Timeout);

      var reloaded = new UsageStats(workspaceDir);
      var u = reloaded.Get("gpt-x");
      Assert.IsNotNull(u);
      Assert.AreEqual(1, u.Requests);
      Assert.AreEqual(1, u.Successes);
      Assert.AreEqual(15, u.TotalTokens);
      Assert.AreEqual(1, u.Failures);
      Assert.AreEqual(1, u.TimeoutErrors);
    }
  }
}
