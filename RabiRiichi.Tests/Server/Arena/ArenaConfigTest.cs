using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Config;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Core;
using System;
using System.Collections.Generic;
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
    public void TestDefaultsAreValidOutOfTheBox() {
      var cfg = new ArenaConfig {
        WorkspaceDir = workspaceDir,
        AdminPassword = "secret",
      };

      Assert.IsNotNull(cfg.GameRules);
      Assert.IsNotNull(cfg.Prompts);
      Assert.IsNotNull(cfg.Decision);
      Assert.IsNotNull(cfg.Run);
      Assert.IsNotNull(cfg.Rating);
      Assert.IsNotNull(cfg.Models);

      Assert.AreEqual(4, cfg.GameRules.PlayerCount);
      Assert.AreEqual(1, cfg.GameRules.TotalRound);
      Assert.AreEqual("Regular", cfg.GameRules.TileSet.Preset);
      Assert.AreEqual(0, cfg.GameRules.TileSet.ExtraWildcards);
      Assert.AreEqual(4, cfg.Run.SwissRounds);
      Assert.AreEqual(8, cfg.Run.TargetMatches);
      Assert.AreEqual(2, cfg.Run.MatchConcurrency);
      Assert.AreEqual(24.0, cfg.Rating.KFactor, 1e-9);
      Assert.AreEqual(1500.0, cfg.Rating.InitialElo, 1e-9);
      Assert.AreEqual(60, cfg.Decision.TimeoutSeconds);
      Assert.AreEqual(2, cfg.Decision.MaxRetries);
      Assert.AreEqual("en", cfg.Prompts.Language);
      Assert.AreEqual("standard", cfg.Prompts.ToolContextMode);
      Assert.IsTrue(cfg.Prompts.AskLlmForCalls);
      Assert.IsFalse(cfg.Prompts.EnableTableTalk);
      Assert.IsFalse(cfg.Prompts.RevealOpponentIdentity);

      var errors = cfg.Validate();
      Assert.AreEqual(0, errors.Count, "Unexpected errors: " + string.Join("; ", errors));
    }

    [TestMethod]
    public void TestLoadPartialFileFillsDefaults() {
      var path = Path.Combine(workspaceDir, "partial.json");
      File.WriteAllText(path,
          "{ \"workspaceDir\": \"./ws\", \"adminPassword\": \"secret\" }");

      var cfg = ArenaConfig.Load(path);
      Assert.AreEqual("./ws", cfg.WorkspaceDir);
      Assert.AreEqual("secret", cfg.AdminPassword);
      Assert.AreEqual(1, cfg.GameRules.TotalRound);
      Assert.AreEqual(4, cfg.GameRules.PlayerCount);
      Assert.AreEqual(1500.0, cfg.Rating.InitialElo, 1e-9);
    }

    [TestMethod]
    public void TestRoundTripSaveLoad_PreservesWildcardsKoyakuAndPrompts() {
      var cfg = new ArenaConfig {
        WorkspaceDir = workspaceDir,
        AdminPassword = "pw",
        WsUrl = "wss://arena.example.com",
        ClientUrl = "https://play.example.com",
        ExposeReasoningPublicly = false,
      };
      cfg.GameRules.PlayerCount = 3;
      cfg.GameRules.TotalRound = 2;
      cfg.GameRules.MinHan = 2;
      cfg.GameRules.TileSet.Preset = "Sanma";
      cfg.GameRules.TileSet.ExtraWildcards = 4;
      cfg.GameRules.AllowedYakus.Add("TsubameGaeshi");
      cfg.Run.ScheduleMode = "balanced";
      cfg.Run.SwissRounds = 5;
      cfg.Run.TargetMatches = 12;
      cfg.Run.MatchConcurrency = 3;
      cfg.Rating.KFactor = 32;
      cfg.Decision.TimeoutSeconds = 120;
      cfg.Prompts.ToolContextMode = "extended";
      cfg.Prompts.RevealOpponentIdentity = true;
      cfg.Prompts.EnableTableTalk = true;
      cfg.Prompts.AskLlmForCalls = false;
      cfg.Prompts.MaxHistoryTurns = 16;
      cfg.Prompts.CustomSystemAppendix = "Play aggressively in East 1.";

      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "claude-x", DisplayName = "Claude X", Provider = "anthropic",
        BaseUrl = "https://api.anthropic.com", Model = "claude-sonnet-4-5",
        ApiKey = "sk-ant-123", ThinkingLevel = "medium", Temperature = 0.5, Enabled = true,
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "baseline-agg", DisplayName = "Rule (aggressive)",
        Provider = "baseline", Variant = "aggressive", FrozenElo = 1525,
        Enabled = true,
      });

      var path = Path.Combine(workspaceDir, "arena.config.json");
      cfg.Save(path);
      Assert.IsTrue(File.Exists(path));

      var loaded = ArenaConfig.Load(path);
      Assert.AreEqual(workspaceDir, loaded.WorkspaceDir);
      Assert.AreEqual("pw", loaded.AdminPassword);
      Assert.AreEqual("wss://arena.example.com", loaded.WsUrl);
      Assert.IsFalse(loaded.ExposeReasoningPublicly);
      Assert.AreEqual(3, loaded.GameRules.PlayerCount);
      Assert.AreEqual(2, loaded.GameRules.TotalRound);
      Assert.AreEqual("Sanma", loaded.GameRules.TileSet.Preset);
      Assert.AreEqual(4, loaded.GameRules.TileSet.ExtraWildcards);
      CollectionAssert.Contains(loaded.GameRules.AllowedYakus, "TsubameGaeshi");
      Assert.AreEqual("balanced", loaded.Run.ScheduleMode);
      Assert.AreEqual(12, loaded.Run.TargetMatches);
      Assert.AreEqual(3, loaded.Run.MatchConcurrency);
      Assert.AreEqual("extended", loaded.Prompts.ToolContextMode);
      Assert.IsTrue(loaded.Prompts.RevealOpponentIdentity);
      Assert.IsTrue(loaded.Prompts.EnableTableTalk);
      Assert.IsFalse(loaded.Prompts.AskLlmForCalls);
      Assert.AreEqual(16, loaded.Prompts.MaxHistoryTurns);

      var claude = loaded.Models.Single(m => m.Id == "claude-x");
      Assert.AreEqual("anthropic", claude.Provider);
      Assert.AreEqual("sk-ant-123", claude.ApiKey);
      Assert.AreEqual("medium", claude.ThinkingLevel);

      var baseline = loaded.Models.Single(m => m.Id == "baseline-agg");
      Assert.AreEqual("baseline", baseline.Provider);
      Assert.AreEqual("aggressive", baseline.Variant);
      Assert.AreEqual(1525.0, baseline.FrozenElo);
    }

    [TestMethod]
    public void TestMetadataAndTileSetBuilder_SupportsAllPresetsWildcardsAndCustom() {
      Assert.AreEqual(73, ArenaMetadata.Yakus.Count);
      Assert.IsTrue(ArenaMetadata.Yakus.Any(y => y.Group == "koyaku"));
      Assert.AreEqual(38, ArenaMetadata.CanonicalTileCodes.Count);

      var regular = new ArenaConfig.TileSetConfig { Preset = "Regular" }.BuildTiles();
      Assert.AreEqual(136, regular.Count);
      Assert.AreEqual(0, regular.Count(t => t.IsWildcard));

      var regWild = new ArenaConfig.TileSetConfig {
        Preset = "RegularWildcards",
        ExtraWildcards = 2,
      }.BuildTiles();
      Assert.AreEqual(142, regWild.Count);
      Assert.AreEqual(6, regWild.Count(t => t.IsWildcard));

      var sanma = new ArenaConfig.TileSetConfig { Preset = "Sanma" }.BuildTiles();
      Assert.AreEqual(108, sanma.Count);
      Assert.AreEqual(0, sanma.Count(t => t.Suit == TileSuit.M && t.Num >= 2 && t.Num <= 8));

      var twoSets = new ArenaConfig.TileSetConfig { Preset = "TwoSets" }.BuildTiles();
      Assert.AreEqual(272, twoSets.Count);

      var custom = new ArenaConfig.TileSetConfig {
        Preset = "Custom",
        ExtraWildcards = 4,
        CustomCounts = ArenaMetadata.CanonicalTileCodes
            .Where(c => c != "*")
            .ToDictionary(c => c, c => c.StartsWith("r5", StringComparison.Ordinal) ? 0 : 4),
      }.BuildTiles();
      Assert.AreEqual(34 * 4 + 4, custom.Count);
      Assert.AreEqual(4, custom.Count(t => t.IsWildcard));
    }

    [TestMethod]
    public void TestRedactedAndMergeSecrets_HandlesHasApiKeyAndClearApiKey() {
      var cfg = new ArenaConfig {
        WorkspaceDir = workspaceDir,
        AdminPassword = "super-secret",
      };
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "gpt-x", DisplayName = "GPT-X", Provider = "openai", Model = "m", ApiKey = "sk-secret",
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "local-llm", DisplayName = "Local", Provider = "openai", Model = "m",
        BaseUrl = "http://127.0.0.1:8000", ApiKey = null,
      });

      var redacted = cfg.Redacted();
      Assert.AreEqual("", redacted.AdminPassword);
      Assert.IsNull(redacted.Models[0].ApiKey);
      Assert.AreEqual(true, redacted.Models[0].HasApiKey);
      Assert.IsNull(redacted.Models[1].ApiKey);
      Assert.AreEqual(false, redacted.Models[1].HasApiKey);

      // Merging a redacted payload back preserves stored secrets.
      var incomingPreserve = cfg.Redacted();
      incomingPreserve.MergeSecretsFrom(cfg);
      Assert.AreEqual("super-secret", incomingPreserve.AdminPassword);
      Assert.AreEqual("sk-secret", incomingPreserve.Models[0].ApiKey);
      Assert.IsNull(incomingPreserve.Models[0].HasApiKey);

      // Setting ClearApiKey = true explicitly deletes the stored key.
      var incomingClear = cfg.Redacted();
      incomingClear.Models[0].ClearApiKey = true;
      incomingClear.MergeSecretsFrom(cfg);
      Assert.IsNull(incomingClear.Models[0].ApiKey);
      Assert.IsNull(incomingClear.Models[0].ClearApiKey);
    }

    [TestMethod]
    public void TestValidateCatchesBadValuesAndUnsafeUrls() {
      var cfg = new ArenaConfig {
        WorkspaceDir = "",
        AdminPassword = "",
        ClientUrl = "javascript:alert(1)",
        WsUrl = "ftp://bad.example.com",
      };
      cfg.GameRules.TotalRound = 3;
      cfg.GameRules.TileSet.ExtraWildcards = 99;
      cfg.Run.SwissRounds = 0;
      cfg.Run.ScheduleMode = "invalid";
      cfg.Rating.KFactor = 0;
      cfg.Decision.TimeoutSeconds = 1;
      cfg.Prompts.ToolContextMode = "magic";
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "bad space!", DisplayName = "Bad", Provider = "openai", Model = "m",
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "dup", DisplayName = "Dup 1", Provider = "openai", Model = "m",
        BaseUrl = "javascript:evil()",
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "dup", DisplayName = "", Provider = "unknown",
      });
      cfg.Models.Add(new ArenaConfig.ModelConfig {
        Id = "no-model", DisplayName = "No Model", Provider = "gemini", Model = "",
      });

      var errors = cfg.Validate();
      Assert.IsTrue(errors.Count >= 10, string.Join(" | ", errors));
      Assert.IsTrue(errors.Any(e => e.Contains("workspaceDir")));
      Assert.IsTrue(errors.Any(e => e.Contains("adminPassword")));
      Assert.IsTrue(errors.Any(e => e.Contains("clientUrl")));
      Assert.IsTrue(errors.Any(e => e.Contains("wsUrl")));
      Assert.IsTrue(errors.Any(e => e.Contains("extraWildcards")));
      Assert.IsTrue(errors.Any(e => e.Contains("swissRounds")));
      Assert.IsTrue(errors.Any(e => e.Contains("scheduleMode")));
      Assert.IsTrue(errors.Any(e => e.Contains("kFactor")));
      Assert.IsTrue(errors.Any(e => e.Contains("timeoutSeconds")));
      Assert.IsTrue(errors.Any(e => e.Contains("toolContextMode")));
      Assert.IsTrue(errors.Any(e => e.Contains("Duplicate model id")));
      Assert.IsTrue(errors.Any(e => e.Contains("baseUrl")));
      Assert.IsTrue(errors.Any(e => e.Contains("requires model name")));
    }

    [TestMethod]
    public void TestValidateRejectsDeckTooSmallForPlayerCount() {
      var cfg = new ArenaConfig {
        WorkspaceDir = workspaceDir,
        AdminPassword = "pw",
      };
      cfg.GameRules.PlayerCount = 4;
      cfg.GameRules.TileSet.Preset = "Custom";
      cfg.GameRules.TileSet.CustomCounts = new Dictionary<string, int> {
        ["1z"] = 4,
        ["2z"] = 4,
      };

      var errors = cfg.Validate();
      Assert.IsTrue(errors.Any(e => e.Contains("Invalid game rules")),
          "Expected engine wall-size validation error, got: " + string.Join("; ", errors));
    }

    [TestMethod]
    public void TestPromptLanguage_RoundTripCloneAndValidation() {
      foreach (var lang in new[] { "en", "zhs", "zht", "ja" }) {
        var cfg = new ArenaConfig {
          WorkspaceDir = workspaceDir,
          AdminPassword = "pw",
        };
        cfg.Prompts.Language = lang;
        cfg.Models.Add(new ArenaConfig.ModelConfig {
          Id = "llm-override",
          DisplayName = "LLM Override",
          Provider = "openai",
          Model = "gpt-5",
          PromptLanguage = lang,
        });
        cfg.Models.Add(new ArenaConfig.ModelConfig {
          Id = "llm-inherit",
          DisplayName = "LLM Inherit",
          Provider = "gemini",
          Model = "gemini-2.5-pro",
          PromptLanguage = "",
        });

        Assert.AreEqual(0, cfg.Validate().Count);

        var path = Path.Combine(workspaceDir, $"arena.{lang}.json");
        cfg.Save(path);
        var loaded = ArenaConfig.Load(path);
        Assert.AreEqual(lang, loaded.Prompts.Language);
        Assert.AreEqual(lang, loaded.Models.Single(m => m.Id == "llm-override").PromptLanguage);
        Assert.AreEqual("", loaded.Models.Single(m => m.Id == "llm-inherit").PromptLanguage);

        var cloned = loaded.Clone();
        Assert.AreEqual(lang, cloned.Prompts.Language);
        Assert.AreEqual(lang, cloned.Models.Single(m => m.Id == "llm-override").PromptLanguage);
      }

      var invalid = new ArenaConfig {
        WorkspaceDir = workspaceDir,
        AdminPassword = "pw",
      };
      invalid.Prompts.Language = "fr";
      invalid.Models.Add(new ArenaConfig.ModelConfig {
        Id = "bad-lang",
        DisplayName = "Bad Lang",
        Provider = "openai",
        Model = "gpt-5",
        PromptLanguage = "ko",
      });

      var errors = invalid.Validate();
      Assert.IsTrue(errors.Any(e => e.Contains("prompts.language")), string.Join(" | ", errors));
      Assert.IsTrue(errors.Any(e => e.Contains("promptLanguage")), string.Join(" | ", errors));
    }
  }
}
