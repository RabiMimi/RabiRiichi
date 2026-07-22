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

    private static LlmSettings MesugakiSettings() =>
        LlmSettings.FromProto(new LlmAiConfig {
          Provider = LlmProvider.Openai,
          ApiToken = "sk",
          Model = "m",
          Language = "en",
          DisplayName = "TestBot",
          PromptTemplate = LlmPromptTemplate.Mesugaki,
        }, out _);

    private static IReadOnlyDictionary<int, string> Names() => new Dictionary<int, string> {
      [0] = "TestBot",
      [1] = "Alice",
      [2] = "小和和", // localized rule-based AI name (matches client)
      [3] = "Bob",
    };

    private static IReadOnlyDictionary<int, LlmSeatRole> Roles() =>
        new Dictionary<int, LlmSeatRole> {
          [0] = LlmSeatRole.Llm,
          [1] = LlmSeatRole.Human,
          [2] = LlmSeatRole.OtherAi,
          [3] = LlmSeatRole.Llm,
        };

    [TestMethod]
    public void SystemPrompt_ListsOpponentsAndSchemaAndLanguage() {
      var builder = new LlmPromptBuilder(Settings("zhs"), Names(), Roles());
      var prompt = builder.BuildSystemPrompt(0);

      StringAssert.Contains(prompt, "TestBot");
      StringAssert.Contains(prompt, "Alice");
      StringAssert.Contains(prompt, "小和和"); // localized AI name, not RULEBASED
      Assert.IsFalse(prompt.Contains("RULEBASED"));
      Assert.IsFalse(prompt.Contains("大叔"));
      Assert.IsFalse(prompt.Contains("ojisan"));
      StringAssert.Contains(prompt, "Alice（人类玩家）");
      StringAssert.Contains(prompt, "Bob（LLM玩家）");
      Assert.IsFalse(prompt.Contains("\"choice\""));
      StringAssert.Contains(prompt, "\"say\"");
      StringAssert.Contains(prompt, "Simplified Chinese");
      StringAssert.Contains(prompt, "m=万子");
      StringAssert.Contains(prompt, "1z=东");
      StringAssert.Contains(prompt, "5z=白");
      StringAssert.Contains(prompt, "自风");
      StringAssert.Contains(prompt, "场风");
      StringAssert.Contains(prompt, "不要混淆以下三种杠");
      StringAssert.Contains(prompt, "ANKAN (暗杠");
      StringAssert.Contains(prompt, "KAKAN (加杠");
      StringAssert.Contains(prompt, "DAIMINKAN (大明杠");
      StringAssert.Contains(prompt, "1 in 5 turns");
      // Every advertised sticker mood is a valid one from the registry.
      foreach (var mood in StickerRegistry.Moods) {
        StringAssert.Contains(prompt, mood);
      }
    }

    [TestMethod]
    public void SystemPrompt_LocalizesTileNotationGuidance() {
      var english = new LlmPromptBuilder(Settings("en"), Names()).BuildSystemPrompt(0);
      StringAssert.Contains(english, "m = characters/manzu");
      StringAssert.Contains(english, "5z = white dragon");
      StringAssert.Contains(english, "seat wind");
      StringAssert.Contains(english, "prevailing round wind");

      var japanese = new LlmPromptBuilder(Settings("ja"), Names()).BuildSystemPrompt(0);
      StringAssert.Contains(japanese, "m=萬子");
      StringAssert.Contains(japanese, "5z=白");
      StringAssert.Contains(japanese, "自風");
      StringAssert.Contains(japanese, "場風");
    }

    [TestMethod]
    public void SystemPrompt_LoadsMesugakiPersona() {
      var prompt = new LlmPromptBuilder(MesugakiSettings(), Names(), Roles())
          .BuildSystemPrompt(0);

      StringAssert.Contains(prompt, "extremely teasing, smug mesugaki");
      StringAssert.Contains(prompt, "use lots of \"~\", \"♡\"");
      StringAssert.Contains(prompt, "Alice-ojisan");
      StringAssert.Contains(prompt, "weakling♡");
      StringAssert.Contains(prompt, "never apply ojisan to other AIs, tiles");
      StringAssert.Contains(prompt, "Alice (human player");
      StringAssert.Contains(prompt, "Bob (LLM player; a cute girl; never call her ojisan)");
      StringAssert.Contains(prompt, "All other LLMs are cute girls");
      Assert.IsFalse(prompt.Contains("cutesy and bubbly like an anime schoolgirl"));
      Assert.IsFalse(prompt.Contains("{{"));
    }

    [TestMethod]
    public void SystemPrompt_LocalizesMesugakiPersonaHint() {
      var config = new LlmAiConfig {
        Provider = LlmProvider.Openai,
        ApiToken = "sk",
        Model = "m",
        Language = "ja",
        PromptTemplate = LlmPromptTemplate.Mesugaki,
      };
      var settings = LlmSettings.FromProto(config, out _);

      var prompt = new LlmPromptBuilder(settings, Names(), Roles()).BuildSystemPrompt(0);

      StringAssert.Contains(prompt, "ざぁこ♡");
      StringAssert.Contains(prompt, "<名前>-おじさん");
      StringAssert.Contains(prompt, "牌・字牌・風・三元牌");
      StringAssert.Contains(prompt, "Bob（LLMプレイヤーのかわいい女の子：「おじさん」と呼ばない）");
      Assert.IsFalse(prompt.Contains("friendly JK"));
    }

    [TestMethod]
    public void InitialSystemPrompt_IncludesGameConfiguration() {
      var game = BuildGame(b => b
          .WithConfig(c => c
              .SetTotalRound(2)
              .SetMinHan(2)
              .SetInitialPoints(35000)
              .SetFinishPoints(40000))
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p")));
      var view = new PublicGameView(game, 0);
      var builder = new LlmPromptBuilder(Settings(), Names());

      var initialPrompt = builder.BuildSystemPrompt(0, view);

      StringAssert.Contains(initialPrompt, "Game configuration (sent once)");
      StringAssert.Contains(initialPrompt, "match length: East-South");
      StringAssert.Contains(initialPrompt, "minimum yaku han to win: 2");
      StringAssert.Contains(initialPrompt, "bonus/dora han do not count");
      StringAssert.Contains(initialPrompt, "Starting points: 35000");
      StringAssert.Contains(initialPrompt, "target points: 40000");
      StringAssert.Contains(initialPrompt, "Initial tile composition:");
      StringAssert.Contains(initialPrompt, "Allowed yaku:");
      Assert.IsFalse(builder.BuildSystemPrompt(0).Contains("Game configuration"));
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
      StringAssert.Contains(prompt, "Alice discards: 1z(East wind)");
      StringAssert.Contains(prompt, "Current round: East 1");
      StringAssert.Contains(prompt, "riichi sticks:");
      StringAssert.Contains(prompt, "current player: not set");
      StringAssert.Contains(prompt, "Round wind (prevailing wind): East");
      StringAssert.Contains(prompt, "Your seat wind: East");
      StringAssert.Contains(prompt, "Current dora indicator(s):");
      StringAssert.Contains(prompt, "Current indicated dora tile(s):");
      StringAssert.Contains(prompt, "Players: TestBot:");
      StringAssert.Contains(prompt, "Alice:");
      StringAssert.Contains(prompt, "Hand status:");
      StringAssert.Contains(prompt, "Wall tiles left:");
      StringAssert.Contains(prompt, "You decided to: discard 1m");
      StringAssert.Contains(prompt, "<trimmed due to length>");
      StringAssert.Contains(prompt, "<sticker: mimi/happy.png>");
      StringAssert.Contains(prompt, "quoted messages from other players");
      StringAssert.Contains(prompt, "Do not repeat their exact wording");
      StringAssert.Contains(prompt, "They then chatted in this order (details omitted): Alice, Bob");
      StringAssert.Contains(prompt, "quiet for at least 10 turns");
      StringAssert.Contains(prompt, "automatically discarded 1m after riichi");
      StringAssert.Contains(prompt, "chatted on 2 or more consecutive turns");
      StringAssert.Contains(prompt, "context, not a request to chat");
      StringAssert.Contains(prompt, "Return say=null and sticker=null this turn");
      StringAssert.Contains(prompt, "{\"say\":null,\"sticker\":null}");
      Assert.IsFalse(prompt.Contains("Speak as though"));
      Assert.IsFalse(prompt.Contains("Choices:"));

      var chinesePrompt = new LlmPromptBuilder(Settings("zhs"), Names())
          .BuildDecisionPrompt(
              view, events, "discard 1m", null, [],
              quietReminder: false, consecutiveChatReminder: false);
      StringAssert.Contains(chinesePrompt, "Alice discards: 1z(东风牌)");
    }

    [TestMethod]
    public void DecisionPrompt_IncludesCurrentTenpaiWaitsAndValue() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1112p")));
      var view = new PublicGameView(game, 0);
      var builder = new LlmPromptBuilder(Settings(), Names());

      var prompt = builder.BuildDecisionPrompt(
          view, [], "discard 9s", null, [],
          quietReminder: false, consecutiveChatReminder: false);

      StringAssert.Contains(prompt, "Hand status: TENPAI");
      StringAssert.Contains(prompt, "2p [");
      StringAssert.Contains(prompt, "han (");
      StringAssert.Contains(prompt, "yaku han");
      StringAssert.Contains(prompt, "unseen");
    }

    [TestMethod]
    public void ConsecutiveChatReminder_TriggersAfterTwoSpeakingTurns() {
      Assert.IsFalse(LlmPromptBuilder.ShouldSendConsecutiveChatReminder(1));
      Assert.IsTrue(LlmPromptBuilder.ShouldSendConsecutiveChatReminder(2));
      Assert.IsTrue(LlmPromptBuilder.ShouldSendConsecutiveChatReminder(3));
    }

    [TestMethod]
    public void BuildEndGamePrompt_IncludesRankingsAndInstruction() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p"))
          .WithPlayer(1, p => p.SetFreeTiles("123456789p1234s"))
          .WithPlayer(2, p => p.SetFreeTiles("123456789s1234m"))
          .WithPlayer(3, p => p.SetFreeTiles("123456789m1234s")));
      var view = new PublicGameView(game, 0);
      var builder = new LlmPromptBuilder(Settings(), Names());

      var endGamePoints = new long[] { 35000, 25000, 20000, 20000 };
      var prompt = builder.BuildEndGamePrompt(
          view,
          ["TestBot wins by tsumo"],
          [new("Alice", "gg", null)],
          endGamePoints);

      StringAssert.Contains(prompt, "GAME OVER - FINAL RANKINGS");
      StringAssert.Contains(prompt, "Rank #1: TestBot (YOU) with 35000 points");
      StringAssert.Contains(prompt, "Rank #2: Alice with 25000 points");
      StringAssert.Contains(prompt, "finished in rank #1 out of 4");
      StringAssert.Contains(prompt, "end-of-game comment");
    }
  }
}
