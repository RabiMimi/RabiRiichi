using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Tests.Scenario;
using System.Collections.Generic;

namespace RabiRiichi.Tests.Server.Agents.Llm {
  [TestClass]
  public class LlmPromptBuilderTest {
    private static Game BuildGame(System.Action<ScenarioBuilder> configure) {
      var builder = new ScenarioBuilder();
      configure(builder);
      Game game = null;
      builder.Build(0).WithGame(g => game = g);
      return game;
    }

    private static LlmSettings Settings(string language = "en", string name = "TestBot") =>
        LlmSettings.FromProto(new LlmAiConfig {
          Provider = LlmProvider.Openai,
          ApiToken = "sk",
          Model = "m",
          Language = language,
          DisplayName = name,
        }, out _);

    private static IReadOnlyDictionary<int, string> Names() => new Dictionary<int, string> {
      [0] = "TestBot",
      [1] = "Alice",
      [2] = "小和和", // localized rule-based AI name (matches client)
      [3] = "Bob",
    };

    [TestMethod]
    public void SystemPrompt_ListsOpponentsAndSchemaAndLanguage() {
      var builder = new LlmPromptBuilder(Settings("zhs"), Names());
      var prompt = builder.BuildSystemPrompt(0);

      StringAssert.Contains(prompt, "TestBot");
      StringAssert.Contains(prompt, "Alice");
      StringAssert.Contains(prompt, "小和和"); // localized AI name, not RULEBASED
      Assert.IsFalse(prompt.Contains("RULEBASED"));
      StringAssert.Contains(prompt, "\"choice\"");
      StringAssert.Contains(prompt, "Simplified Chinese");
      // Every advertised sticker mood is a valid one from the registry.
      foreach (var mood in StickerRegistry.Moods) {
        StringAssert.Contains(prompt, mood);
      }
    }

    [TestMethod]
    public void RoundHeader_IncludesHandDoraAndPoints() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p")));
      var view = new PublicGameView(game, 0);
      var builder = new LlmPromptBuilder(Settings(), Names());

      var header = builder.BuildRoundHeader(view);
      StringAssert.Contains(header, "New round");
      StringAssert.Contains(header, "Your hand");
      StringAssert.Contains(header, "Points");
    }

    [TestMethod]
    public void DecisionPrompt_IncludesEventsAndNumberedChoices() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p"))
          .WithPlayer(1, p => p.SetDiscarded(1, "1z")));
      var view = new PublicGameView(game, 0);
      var builder = new LlmPromptBuilder(Settings(), Names());

      var menu = new List<LlmChoice> {
        new() { Id = 0, Kind = "skip", Description = "Do nothing / pass" },
        new() { Id = 1, Kind = "discard", Description = "discard 1m" },
      };
      var events = new List<string> { "Alice discards 1z (from hand)" };

      var prompt = builder.BuildDecisionPrompt(view, events, menu);
      StringAssert.Contains(prompt, "Recent events");
      StringAssert.Contains(prompt, "Alice discards 1z");
      StringAssert.Contains(prompt, "0: Do nothing");
      StringAssert.Contains(prompt, "1: discard 1m");
      StringAssert.Contains(prompt, "Choices:");
    }
  }
}
