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
      Assert.IsFalse(prompt.Contains("\"choice\""));
      StringAssert.Contains(prompt, "\"say\"");
      StringAssert.Contains(prompt, "Simplified Chinese");
      StringAssert.Contains(prompt, "1 in 5 turns");
      // Every advertised sticker mood is a valid one from the registry.
      foreach (var mood in StickerRegistry.Moods) {
        StringAssert.Contains(prompt, mood);
      }
    }

    [TestMethod]
    public void RoundHeader_IncludesHandDoraAndPoints() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p"))
          .WithWall(w => w.AddDoras("9m")));
      var view = new PublicGameView(game, 0);
      var builder = new LlmPromptBuilder(Settings(), Names());

      var header = builder.BuildRoundHeader(view);
      StringAssert.Contains(header, "New round");
      StringAssert.Contains(header, "Your hand");
      StringAssert.Contains(header, "Points");
      StringAssert.Contains(header, "Dora indicator(s): 9m");
      StringAssert.Contains(header, "Indicated dora tile(s): 1m");
    }

    [TestMethod]
    public void DecisionPrompt_IncludesSelectedActionChatsAndReminder() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p"))
          .WithPlayer(1, p => p.SetDiscarded(1, "1z")));
      var view = new PublicGameView(game, 0);
      var builder = new LlmPromptBuilder(Settings(), Names());

      var events = new List<string> { "Alice discards 1z (from hand)" };
      var longChat = new string('x', 300);
      var chats = new List<LlmChatEntry>();
      for (var i = 0; i < 12; i++) {
        chats.Add(new LlmChatEntry(i % 2 == 0 ? "Alice" : "Bob",
            i == 0 ? longChat : $"message {i}", i == 1 ? "mimi/happy.png" : null));
      }

      var prompt = builder.BuildDecisionPrompt(
          view, events, "discard 1m",
          "You automatically discarded 1m after riichi because that was the only valid action.",
          chats, quietReminder: true, consecutiveChatReminder: true);
      StringAssert.Contains(prompt, "Recent events");
      StringAssert.Contains(prompt, "Alice discards 1z");
      StringAssert.Contains(prompt, "You decided to: discard 1m");
      StringAssert.Contains(prompt, "<trimmed due to length>");
      StringAssert.Contains(prompt, "<sticker: mimi/happy.png>");
      StringAssert.Contains(prompt, "They then chatted in this order (details omitted): Alice, Bob");
      StringAssert.Contains(prompt, "quiet for at least 10 turns");
      StringAssert.Contains(prompt, "automatically discarded 1m after riichi");
      StringAssert.Contains(prompt, "chatted on 2 or more consecutive turns");
      Assert.IsFalse(prompt.Contains("Choices:"));
    }

    [TestMethod]
    public void ConsecutiveChatReminder_TriggersAfterTwoSpeakingTurns() {
      Assert.IsFalse(LlmPromptBuilder.ShouldSendConsecutiveChatReminder(1));
      Assert.IsTrue(LlmPromptBuilder.ShouldSendConsecutiveChatReminder(2));
      Assert.IsTrue(LlmPromptBuilder.ShouldSendConsecutiveChatReminder(3));
    }
  }
}
