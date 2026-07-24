using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Storage;
using System;
using System.IO;
using System.Linq;

namespace RabiRiichi.Tests.Server.Arena {
  [TestClass]
  public class ArenaConfigTest {
    private string workspaceDir;

    [TestInitialize]
    public void Setup() {
      workspaceDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_cfg_{Guid.NewGuid():N}");
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
    public void TestLoadMissingFileReturnsDefaults() {
      var path = Path.Combine(workspaceDir, "does-not-exist.json");
      var cfg = ArenaConfig.Load(path);

      Assert.IsNotNull(cfg);
      Assert.IsNotNull(cfg.Run);
      Assert.IsNotNull(cfg.Rating);
      Assert.IsNotNull(cfg.Decision);
      Assert.IsNotNull(cfg.Exposure);
      Assert.IsNotNull(cfg.Models);
      Assert.AreEqual(0, cfg.Models.Count);

      // Sane defaults from §13.
      Assert.AreEqual(7, cfg.Run.SwissRounds);
      Assert.AreEqual(2, cfg.Run.TotalRound);
      Assert.AreEqual(4, cfg.Run.PlayerCount);
      Assert.AreEqual(24, cfg.Rating.KFactor);
      Assert.AreEqual(1500, cfg.Rating.InitialElo);
      Assert.AreEqual(180, cfg.Decision.TimeoutSeconds);
      Assert.AreEqual(3, cfg.Decision.MaxRetries);
      Assert.IsFalse(cfg.Exposure.RevealOpponentIdentity);
      Assert.IsFalse(cfg.Exposure.ChatToAgents);
    }

    [TestMethod]
    public void TestLoadPartialFileFillsDefaults() {
      var path = Path.Combine(workspaceDir, "partial.json");
      // Only some fields specified; nested blocks omitted.
      File.WriteAllText(path,
          "{ \"workspaceDir\": \"/tmp/ws\", \"adminPassword\": \"secret\" }");

      var cfg = ArenaConfig.Load(path);
      Assert.AreEqual("/tmp/ws", cfg.WorkspaceDir);
      Assert.AreEqual("secret", cfg.AdminPassword);
      // Nested blocks defaulted.
      Assert.AreEqual(2, cfg.Run.TotalRound);
      Assert.AreEqual(1500, cfg.Rating.InitialElo);
    }

    [TestMethod]
    public void TestRoundTripSaveLoad() {
      var cfg = new ArenaConfig {
        WorkspaceDir = "/var/rabiriichi-arena",
        AdminPassword = "pw",
        PublicUrl = "https://arena.example.com",
        WsUrl = "wss://arena.example.com",
        ClientUrl = "https://play.example.com",
      };
      cfg.Run.SwissRounds = 5;
      cfg.Run.TotalRound = 1;
      cfg.Rating.KFactor = 32;
      cfg.Decision.TimeoutSeconds = 300;
      cfg.Exposure.RevealOpponentIdentity = true;
      cfg.Exposure.ChatToAgents = true;
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "gpt-x", DisplayName = "GPT-X", Provider = "openai",
        BaseUrl = "https://api.openai.com", Model = "gpt-x",
        ApiKey = "sk-123", Thinking = true, Enabled = true,
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "baseline-mid", DisplayName = "Rule-based (mid)",
        Provider = "baseline", Variant = "default", FrozenElo = 1500,
        Enabled = true,
      });

      var path = Path.Combine(workspaceDir, "arena.config.json");
      cfg.Save(path);
      Assert.IsTrue(File.Exists(path));

      var loaded = ArenaConfig.Load(path);
      Assert.AreEqual("/var/rabiriichi-arena", loaded.WorkspaceDir);
      Assert.AreEqual("pw", loaded.AdminPassword);
      Assert.AreEqual("wss://arena.example.com", loaded.WsUrl);
      Assert.AreEqual(5, loaded.Run.SwissRounds);
      Assert.AreEqual(1, loaded.Run.TotalRound);
      Assert.AreEqual(32, loaded.Rating.KFactor);
      Assert.AreEqual(300, loaded.Decision.TimeoutSeconds);
      Assert.IsTrue(loaded.Exposure.RevealOpponentIdentity);
      Assert.IsTrue(loaded.Exposure.ChatToAgents);
      Assert.AreEqual(2, loaded.Models.Count);

      var gpt = loaded.Models.Single(m => m.Id == "gpt-x");
      Assert.AreEqual("openai", gpt.Provider);
      Assert.AreEqual("sk-123", gpt.ApiKey);
      Assert.IsTrue(gpt.Thinking);

      var baseline = loaded.Models.Single(m => m.Id == "baseline-mid");
      Assert.AreEqual("baseline", baseline.Provider);
      Assert.AreEqual(1500, baseline.FrozenElo);
    }

    [TestMethod]
    public void TestValidateAcceptsGoodConfig() {
      var cfg = new ArenaConfig {
        WorkspaceDir = "/tmp/ws",
        AdminPassword = "pw",
      };
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "gpt-x", Provider = "openai", Model = "gpt-x",
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "baseline-mid", Provider = "baseline", FrozenElo = 1500,
      });

      var errors = cfg.Validate();
      Assert.AreEqual(0, errors.Count,
          "Unexpected errors: " + string.Join("; ", errors));
    }

    [TestMethod]
    public void TestValidateCatchesBadValues() {
      var cfg = new ArenaConfig {
        WorkspaceDir = "",   // empty
        AdminPassword = "",  // empty
      };
      cfg.Run.TotalRound = 3;   // invalid
      cfg.Run.SwissRounds = 0;  // invalid
      cfg.Rating.KFactor = 0;   // invalid
      cfg.Decision.TimeoutSeconds = -1; // invalid
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "", Provider = "unknown", // empty id + bad provider
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "dup", Provider = "openai", Model = "m",
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "dup", Provider = "baseline", // duplicate id + missing frozenElo
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "no-model", Provider = "gemini", // missing model name
      });

      var errors = cfg.Validate();
      Assert.IsTrue(errors.Count > 0);
      Assert.IsTrue(errors.Any(e => e.Contains("workspaceDir")));
      Assert.IsTrue(errors.Any(e => e.Contains("adminPassword")));
      Assert.IsTrue(errors.Any(e => e.Contains("totalRound")));
      Assert.IsTrue(errors.Any(e => e.Contains("swissRounds")));
      Assert.IsTrue(errors.Any(e => e.Contains("kFactor")));
      Assert.IsTrue(errors.Any(e => e.Contains("timeoutSeconds")));
      Assert.IsTrue(errors.Any(e => e.Contains("provider")));
      Assert.IsTrue(errors.Any(e => e.Contains("duplicated")));
      Assert.IsTrue(errors.Any(e => e.Contains("frozenElo")));
      Assert.IsTrue(errors.Any(e => e.Contains("model must not be empty")));
    }

    [TestMethod]
    public void TestRedactedBlanksSecrets() {
      var cfg = new ArenaConfig {
        WorkspaceDir = "/tmp/ws",
        AdminPassword = "super-secret",
      };
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "gpt-x", Provider = "openai", Model = "m", ApiKey = "sk-secret",
      });

      var redacted = cfg.Redacted();
      Assert.AreEqual("", redacted.AdminPassword);
      Assert.AreEqual("", redacted.Models[0].ApiKey);

      // Original is untouched (deep copy).
      Assert.AreEqual("super-secret", cfg.AdminPassword);
      Assert.AreEqual("sk-secret", cfg.Models[0].ApiKey);

      // Non-secret fields preserved in the redacted copy.
      Assert.AreEqual("/tmp/ws", redacted.WorkspaceDir);
      Assert.AreEqual("gpt-x", redacted.Models[0].Id);
    }
  }
}
