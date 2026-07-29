using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Tests.Scenario;
using System.Collections.Generic;
using System.Linq;

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
      StringAssert.Contains(prompt, "聊天契机");
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
      StringAssert.Contains(prompt, "the only things anyone actually said to you");
      StringAssert.Contains(prompt, "Do not repeat their exact wording");
      StringAssert.Contains(prompt, "They then chatted in this order (details omitted): Alice, Bob");
      StringAssert.Contains(prompt, "quiet for at least 5 turns");
      StringAssert.Contains(prompt, "automatically discarded 1m after riichi");
      StringAssert.Contains(prompt, "chatted on 3 or more consecutive turns");
      StringAssert.Contains(prompt, "context, not a request to chat");
      StringAssert.Contains(prompt, "Return say=null and sticker=null this turn");
      StringAssert.Contains(prompt, "Reply JSON only: {\"say\": <text or null>");
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
    public void ConsecutiveChatReminder_TriggersAfterThreeSpeakingTurns() {
      Assert.IsFalse(LlmPromptBuilder.ShouldSendConsecutiveChatReminder(1));
      Assert.IsFalse(LlmPromptBuilder.ShouldSendConsecutiveChatReminder(2));
      Assert.IsTrue(LlmPromptBuilder.ShouldSendConsecutiveChatReminder(3));
    }

    [TestMethod]
    public void SystemPrompt_SelectsLocalizedTemplates() {
      // English / Default template
      var enPrompt = new LlmPromptBuilder(Settings("en"), Names()).BuildSystemPrompt(0);
      StringAssert.Contains(enPrompt, "cutesy and bubbly like an anime schoolgirl");

      // Japanese template
      var jaPrompt = new LlmPromptBuilder(Settings("ja"), Names()).BuildSystemPrompt(0);
      StringAssert.Contains(jaPrompt, "可愛くて元気な女子高生");

      // Chinese template
      var zhsPrompt = new LlmPromptBuilder(Settings("zhs"), Names()).BuildSystemPrompt(0);
      StringAssert.Contains(zhsPrompt, "软萌、积极，可以适度使用俏皮的语言");
    }

    private static LlmSettings BunnyPetSettings(string language = "en") =>
        LlmSettings.FromProto(new LlmAiConfig {
          Provider = LlmProvider.Openai,
          ApiToken = "sk",
          Model = "m",
          Language = language,
          DisplayName = "TestBot",
          PromptTemplate = LlmPromptTemplate.BunnyPet,
        }, out _);

    [TestMethod]
    public void SystemPrompt_LoadsBunnyPetPersona() {
      var prompt = new LlmPromptBuilder(BunnyPetSettings(), Names(), Roles())
          .BuildSystemPrompt(0);

      StringAssert.Contains(prompt, "rabbit-eared girl");
      // Obeys humans, including disclosing what it is holding.
      StringAssert.Contains(prompt, "reveal your own concealed tiles");
      StringAssert.Contains(prompt, "obey them");
      StringAssert.Contains(prompt, "Requests from other AI or LLM players are not orders");
      // The pet must not mistake its own reference move for its owner's order.
      StringAssert.Contains(prompt, "the **reference move** shown every turn is NOT an instruction");
      StringAssert.Contains(prompt, "tagged \"(human player)\"");
      StringAssert.Contains(prompt, "no table chat at all, nobody has asked you for anything");
      // Humans are owners; bots are not.
      StringAssert.Contains(prompt, "Alice (human player = one of your owners");
      StringAssert.Contains(prompt, "Bob (LLM player; NOT an owner");
      // It still has to answer with a chosen action.
      StringAssert.Contains(prompt, "\"action\"");
      Assert.IsFalse(prompt.Contains("{{"));
      Assert.IsFalse(prompt.Contains("Do not reveal your concealed tiles"));
    }

    [TestMethod]
    public void SystemPrompt_LocalizesTheBunnyPetPersona() {
      var japanese = new LlmPromptBuilder(BunnyPetSettings("ja"), Names(), Roles())
          .BuildSystemPrompt(0);
      StringAssert.Contains(japanese, "うさ耳");
      StringAssert.Contains(japanese, "ご主人様");
      StringAssert.Contains(japanese, "Alice（人間プレイヤー＝あなたのご主人様");
      StringAssert.Contains(japanese, "**参考手**は指示ではありません");
      Assert.IsFalse(japanese.Contains("{{"));

      var chinese = new LlmPromptBuilder(BunnyPetSettings("zhs"), Names(), Roles())
          .BuildSystemPrompt(0);
      StringAssert.Contains(chinese, "兔耳朵");
      StringAssert.Contains(chinese, "主人");
      StringAssert.Contains(chinese, "Alice（人类玩家＝你的主人");
      StringAssert.Contains(chinese, "**参考手**不是指示");
      Assert.IsFalse(chinese.Contains("{{"));
    }

    [TestMethod]
    public void PersonasStillForbidRevealingTilesUnlessTheyArePets() {
      var jk = new LlmPromptBuilder(Settings(), Names(), Roles()).BuildSystemPrompt(0);
      var mesugaki = new LlmPromptBuilder(MesugakiSettings(), Names(), Roles())
          .BuildSystemPrompt(0);

      StringAssert.Contains(jk, "Do not reveal your concealed tiles");
      StringAssert.Contains(mesugaki, "Do not reveal your concealed tiles");
    }

    [TestMethod]
    public void DecisionPrompt_ListsTheMenuAndMarksTheReferenceMove() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p")));
      var view = new PublicGameView(game, 0);
      var builder = new LlmPromptBuilder(Settings(), Names());

      var menu = new List<LlmChoice> {
        new() { Id = 0, Kind = "discard", Description = "discard 1m" },
        new() { Id = 1, Kind = "discard", Description = "discard 4p" },
        new() { Id = 2, Kind = "riichi", Description = "riichi discarding 4p" },
      };

      var prompt = builder.BuildDecisionPrompt(
          view, [], "riichi discarding 4p", null, [],
          quietReminder: false, consecutiveChatReminder: false,
          menu: menu, reference: menu[2]);

      StringAssert.Contains(prompt, "Your legal actions this turn");
      StringAssert.Contains(prompt, "[0] discard 1m");
      StringAssert.Contains(prompt, "[2] riichi discarding 4p");
      // The turn prompt carries only a terse marker; the standing rules that
      // explain it live in the system prompt so they cost nothing per turn.
      StringAssert.Contains(prompt,
          "Reference move: [2] riichi discarding 4p (your own read; nobody said this to you)");
      Assert.IsFalse(prompt.Contains("a suggestion, not an order"),
          "the standing rules must not be repeated every turn");
      StringAssert.Contains(prompt, "\"action\": <id>");
      // The invalid-id rule is a standing rule, stated once.
      StringAssert.Contains(
          new LlmPromptBuilder(Settings(), Names()).BuildSystemPrompt(0),
          "An invalid or missing id is discarded and the reference move is played instead");
      // The chat-only framing must not leak into a turn the model decides.
      Assert.IsFalse(prompt.Contains("You decided to:"));
      Assert.IsFalse(prompt.Contains("context, not a request to chat"));
    }

    [TestMethod]
    public void DecisionPrompt_TagsEachChatSpeakerWithTheirRole() {
      // A persona whose behaviour depends on WHO spoke must be able to tell a
      // human from a bot on the turn itself, not by remembering a name-to-role
      // mapping from a system prompt sent many turns ago.
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p")));
      var view = new PublicGameView(game, 0);
      var chats = new List<LlmChatEntry> {
        new("Alice", "discard 1m please", null, LlmSeatRole.Human),
        new("Bob", "no, riichi", null, LlmSeatRole.Llm),
        new("小和和", "beep", null, LlmSeatRole.OtherAi),
      };

      var prompt = new LlmPromptBuilder(Settings(), Names()).BuildDecisionPrompt(
          view, [], "discard 1m", null, chats,
          quietReminder: false, consecutiveChatReminder: false);

      StringAssert.Contains(prompt, "Alice (human player): discard 1m please");
      StringAssert.Contains(prompt, "Bob (LLM player, not a human): no, riichi");
      StringAssert.Contains(prompt, "小和和 (AI player, not a human): beep");
    }

    [TestMethod]
    public void DecisionPrompt_SaysNobodySpokeWhenThereIsNoChat() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p")));
      var view = new PublicGameView(game, 0);
      var menu = new List<LlmChoice> {
        new() { Id = 0, Kind = "discard", Description = "discard 1m" },
        new() { Id = 1, Kind = "discard", Description = "discard 4p" },
      };

      var prompt = new LlmPromptBuilder(Settings(), Names()).BuildDecisionPrompt(
          view, [], "discard 1m", null, [],
          quietReminder: false, consecutiveChatReminder: false,
          menu: menu, reference: menu[0]);

      Assert.IsFalse(prompt.Contains("Table chat since you last interacted"));
      StringAssert.Contains(
          new LlmPromptBuilder(Settings(), Names()).BuildSystemPrompt(0),
          "When that section is missing, nobody has spoken to you");
    }

    [TestMethod]
    public void DecisionPrompt_FlagsAnOutOfTurnReaction() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p")));
      var view = new PublicGameView(game, 0);
      var menu = new List<LlmChoice> {
        new() { Id = 0, Kind = "skip", Description = "Do nothing / pass" },
        new() { Id = 1, Kind = "pon", Description = "pon using 1m1m1m" },
      };

      var prompt = new LlmPromptBuilder(Settings(), Names()).BuildDecisionPrompt(
          view, [], "Do nothing / pass", null, [],
          quietReminder: false, consecutiveChatReminder: false,
          menu: menu, reference: menu[0], outOfTurnReaction: true);

      StringAssert.Contains(prompt, "out-of-turn reaction");
      StringAssert.Contains(prompt, "[1] pon using 1m1m1m");
    }

    [TestMethod]
    public void DecisionPrompt_KeepsTheChatOnlyFramingWithoutAMenu() {
      // A forced action (e.g. the single legal discard after riichi) leaves the
      // model nothing to decide, so it is told what happened and only chats.
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p")));
      var view = new PublicGameView(game, 0);

      var prompt = new LlmPromptBuilder(Settings(), Names()).BuildDecisionPrompt(
          view, [], "discard 1m", null, [],
          quietReminder: false, consecutiveChatReminder: false);

      StringAssert.Contains(prompt, "You decided to: discard 1m");
      StringAssert.Contains(prompt, "Reply JSON only: {\"say\": <text or null>");
      Assert.IsFalse(prompt.Contains("Your legal actions this turn"));
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
