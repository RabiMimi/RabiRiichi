using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Arena.Agents;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;
using RabiRiichi.Tests.Scenario;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProtoLlmProvider = RabiRiichi.Server.Generated.Rpc.LlmProvider;

namespace RabiRiichi.Tests.Server.Arena {
  internal sealed class FakeLlmProvider : ILlmProvider {
    private readonly Queue<LlmResult> results;
    public readonly List<IReadOnlyList<LlmMessage>> Calls = new();

    public FakeLlmProvider(params LlmResult[] scripted) {
      results = new Queue<LlmResult>(scripted);
    }

    public ProtoLlmProvider Provider => ProtoLlmProvider.Openai;

    public int CallCount => Calls.Count;

    public string LastPrompt => Calls.Count == 0
        ? ""
        : string.Join("\n", Calls[^1].Select(m => m.Content));

    public Task<LlmResult> CompleteAsync(
        IReadOnlyList<LlmMessage> messages, int maxOutputTokens, CancellationToken ct) {
      Calls.Add(messages.ToList());
      var r = results.Count > 0 ? results.Dequeue() : LlmResult.Fail("no more scripted results");
      return Task.FromResult(r);
    }
  }

  internal sealed class TestableArenaLlmAI : ArenaLlmAI {
    public TestableArenaLlmAI(
        int id, Room room, ArenaConfig.ModelConfig model,
        ArenaConfig.DecisionConfig decision, ArenaConfig.PromptConfig prompts,
        ILlmProvider provider, ReasoningStore reasoning, UsageStats usage,
        string gameId, Func<int, Wind> windOf, Func<int, string> displayNameOf = null)
        : base(id, room, model, decision, prompts, provider, reasoning, usage,
            gameId, windOf, displayNameOf, UserStatus.InRoom) { }

    public InquiryResponse Run(
        MultiPlayerInquiry gameInquiry, SinglePlayerInquiry playerInquiry,
        TimeSpan timeout) => Decide(gameInquiry, playerInquiry, timeout);
  }

  [TestClass]
  public class ArenaLlmAITest {
    private string workspaceDir;

    [TestInitialize]
    public void Setup() {
      workspaceDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_llm_{Guid.NewGuid():N}");
      Directory.CreateDirectory(workspaceDir);
    }

    [TestCleanup]
    public void Cleanup() {
      if (Directory.Exists(workspaceDir)) {
        try { Directory.Delete(workspaceDir, recursive: true); } catch { }
      }
    }

    private static ArenaConfig.ModelConfig Model(
        string id = "gpt-x", string displayName = "GPT-X") => new() {
      Id = id,
      DisplayName = displayName,
      Provider = "openai",
      Model = "gpt-x-model",
    };

    private static ArenaConfig.DecisionConfig Decision(int maxRetries = 2) => new() {
      TimeoutSeconds = 60,
      MaxRetries = maxRetries,
      MaxOutputTokens = 256,
    };

    private static ArenaConfig.PromptConfig Prompts(
        bool revealIdentity = false,
        bool enableTableTalk = false,
        bool askLlmForCalls = true,
        int maxHistoryTurns = 24) => new() {
      RevealOpponentIdentity = revealIdentity,
      EnableTableTalk = enableTableTalk,
      AskLlmForCalls = askLlmForCalls,
      MaxHistoryTurns = maxHistoryTurns,
    };

    private static Game DiscardTurnGame() {
      var builder = new ScenarioBuilder();
      builder.WithPlayer(0, p => p.SetFreeTiles("123m456p789s1234s"));
      Game game = null;
      builder.Build(0).WithGame(g => game = g);
      var player = game.GetPlayer(0);
      var draw = new GameTile(new Tile("7z"), 999) { player = player };
      player.hand.pendingTile = draw;
      return game;
    }

    private static (MultiPlayerInquiry gi, SinglePlayerInquiry pi, PlayTileAction play)
        DiscardInquiry(Game game) {
      var player = game.GetPlayer(0);
      var hand14 = new List<GameTile>(player.hand.freeTiles) { player.hand.pendingTile };
      var play = new PlayTileAction(player, hand14, hand14[0]);
      var pi = new SinglePlayerInquiry(0);
      pi.AddAction(play);
      pi.DisableSkip();
      var gi = new MultiPlayerInquiry(game);
      return (gi, pi, play);
    }

    private static Wind WindOf(Game game, int seat) => game.GetPlayer(seat).Wind;

    private TestableArenaLlmAI BuildAgent(
        Room room, Game game, FakeLlmProvider provider,
        ArenaConfig.PromptConfig prompts, out ReasoningStore reasoning,
        out UsageStats usage, string modelId = "gpt-x", string gameId = "game-1",
        int maxRetries = 2, bool revealNames = false) {
      reasoning = new ReasoningStore(workspaceDir);
      usage = new UsageStats(workspaceDir);
      var agent = new TestableArenaLlmAI(
          -101, room, Model(modelId), Decision(maxRetries), prompts, provider,
          reasoning, usage, gameId, s => WindOf(game, s),
          revealNames ? _ => "GPT-X" : (Func<int, string>)null);
      room.AddPlayer(agent);
      return agent;
    }

    private static Room NewRoom() =>
        new(new Random(0), new GameConfig { playerCount = 4 });

    // ----- Tests -----------------------------------------------------------

    [TestMethod]
    public void ValidResponseFirstAttempt_ReturnedAndTelemetryRecorded() {
      var game = DiscardTurnGame();
      var (gi, pi, play) = DiscardInquiry(game);
      var provider = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"cut the isolated honor\"}", 120, 30, 15));

      var room = NewRoom();
      var agent = BuildAgent(room, game, provider, Prompts(), out var reasoning, out var usage);

      var resp = agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      Assert.AreEqual(pi.actions.IndexOf(play), resp.index);
      Assert.AreEqual(1, agent.DecisionsCount);
      Assert.AreEqual(1, agent.ValidFirstTryCount);
      Assert.AreEqual(0, agent.RetryCount);
      Assert.AreEqual(0, agent.PenaltyCount);
      Assert.AreEqual(120, agent.PromptTokens);
      Assert.AreEqual(30, agent.CompletionTokens);
      Assert.AreEqual(15, agent.ReasoningTokens);

      var u = usage.Get("gpt-x");
      Assert.IsNotNull(u);
      Assert.AreEqual(1, u.Requests);
      Assert.AreEqual(1, u.Successes);
      Assert.AreEqual(0, u.Penalties);

      var turns = reasoning.ReadTurns("game-1", 0);
      Assert.AreEqual(1, turns.Count);
      Assert.IsTrue(turns[0].Valid);
      Assert.IsFalse(turns[0].Penalized);
      Assert.AreEqual("cut the isolated honor", turns[0].Rationale);
    }

    [TestMethod]
    public void SingleOptionMenu_FastPathsWithoutCallingLlmProvider() {
      var game = DiscardTurnGame();
      var player = game.GetPlayer(0);
      // Forced Riichi tsumogiri: only 1 legal tile in PlayTileAction and skip disabled.
      var onlyTile = new List<GameTile> { player.hand.pendingTile };
      var forcedPlay = new PlayTileAction(player, onlyTile, onlyTile[0]);
      var pi = new SinglePlayerInquiry(0);
      pi.AddAction(forcedPlay);
      pi.DisableSkip();
      var gi = new MultiPlayerInquiry(game);

      var provider = new FakeLlmProvider(); // 0 scripted responses!
      var room = NewRoom();
      var agent = BuildAgent(room, game, provider, Prompts(), out _, out _);

      var resp = agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      Assert.AreEqual(0, provider.CallCount, "Single-choice menu must not call LLM API.");
      Assert.AreEqual(1, agent.AutoDecisionsCount);
      Assert.AreEqual(0, agent.DecisionsCount);
      Assert.AreEqual(pi.actions.IndexOf(forcedPlay), resp.index);
    }

    [TestMethod]
    public void AskLlmForCallsFalse_SkipsOutOfTurnPonMenuWithoutCallingLlm() {
      var game = DiscardTurnGame();
      var p0 = game.GetPlayer(0);
      var p1 = game.GetPlayer(1);
      var discardedTile = new GameTile(new Tile("1m"), 500) { player = p1 };
      // Give p0 a second 1m so Pon is constructible.
      var extra1m = new GameTile(new Tile("1m"), 501) { player = p0 };
      p0.hand.freeTiles.Add(extra1m);
      var pon = new PonAction(0, new List<List<GameTile>> {
        new() { p0.hand.freeTiles[0], extra1m, discardedTile },
      });
      var pi = new SinglePlayerInquiry(0);
      pi.AddAction(pon); // Skip remains enabled on reactive call inquiries.
      var gi = new MultiPlayerInquiry(game);

      var provider = new FakeLlmProvider();
      var room = NewRoom();
      var agent = BuildAgent(
          room, game, provider, Prompts(askLlmForCalls: false), out _, out _);

      var resp = agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      Assert.AreEqual(0, provider.CallCount, "Out-of-turn call should fast-pass when AskLlmForCalls=false.");
      Assert.AreEqual(-1, resp.index);
      Assert.AreEqual(1, agent.AutoDecisionsCount);
    }

    [TestMethod]
    public void BoundedHistory_TrimsToMaxHistoryTurns() {
      var game = DiscardTurnGame();
      var provider = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"turn-one-reason\"}"),
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"turn-two-reason\"}"),
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"turn-three-reason\"}"));

      var room = NewRoom();
      var agent = BuildAgent(
          room, game, provider, Prompts(maxHistoryTurns: 1), out _, out _);

      var (gi, pi, _) = DiscardInquiry(game);
      agent.Run(gi, pi, TimeSpan.FromSeconds(30));
      agent.Run(gi, pi, TimeSpan.FromSeconds(30));
      agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      // Call #3 should carry System + 1 condensed prior turn (2 messages) + current User turn = 4 messages,
      // with turn 1 evicted and turn 2 retained!
      var thirdCall = provider.Calls[2];
      Assert.AreEqual(4, thirdCall.Count);
      var joined = string.Join("\n", thirdCall.Select(m => m.Content));
      Assert.IsFalse(joined.Contains("turn-one-reason"), "Evicted turn 1 must not remain in context");
      StringAssert.Contains(joined, "turn-two-reason");
    }

    [TestMethod]
    public void InvalidAllAttempts_ReturnsDefaultAndPenalizes() {
      var game = DiscardTurnGame();
      var (gi, pi, _) = DiscardInquiry(game);
      var provider = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 99, \"rationale\": \"a\"}"),
          LlmResult.Ok("{\"action\": 88, \"rationale\": \"b\"}"),
          LlmResult.Ok("{\"action\": 77, \"rationale\": \"c\"}"));

      var room = NewRoom();
      var agent = BuildAgent(
          room, game, provider, Prompts(), out var reasoning, out var usage, maxRetries: 2);

      var resp = agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      Assert.AreEqual(-1, resp.index);
      Assert.AreEqual(3, provider.CallCount);
      Assert.AreEqual(1, agent.PenaltyCount);
      Assert.AreEqual(2, agent.RetryCount);

      var u = usage.Get("gpt-x");
      Assert.AreEqual(3, u.Requests);
      Assert.AreEqual(1, u.Penalties);
      Assert.AreEqual(2, u.Retries);

      var turns = reasoning.ReadTurns("game-1", 0);
      Assert.AreEqual(1, turns.Count);
      Assert.IsFalse(turns[0].Valid);
      Assert.IsTrue(turns[0].Penalized);
      Assert.AreEqual(3, turns[0].Attempts);
    }

    [TestMethod]
    public void InvalidThenValid_ReturnedWithRetryCounted() {
      var game = DiscardTurnGame();
      var (gi, pi, play) = DiscardInquiry(game);
      var provider = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 99, \"rationale\": \"bad\"}"),
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"good\"}"));

      var room = NewRoom();
      var agent = BuildAgent(room, game, provider, Prompts(), out var reasoning, out var usage);

      var resp = agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      Assert.AreEqual(pi.actions.IndexOf(play), resp.index);
      Assert.AreEqual(2, provider.CallCount);
      Assert.AreEqual(1, usage.Get("gpt-x").Retries);
      Assert.IsTrue(provider.Calls[^1].Any(m => m.Content.Contains("REJECTED")));
      Assert.IsTrue(reasoning.ReadTurns("game-1", 0)[0].Valid);
    }

    [TestMethod]
    public void Anonymized_PromptHasNoOpponentIdentityAndUsesSeatLabels() {
      var game = DiscardTurnGame();
      var (gi, pi, _) = DiscardInquiry(game);
      var provider = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"x\"}"));

      var room = NewRoom();
      var reasoning = new ReasoningStore(workspaceDir);
      var usage = new UsageStats(workspaceDir);
      var agent = new TestableArenaLlmAI(
          -101, room, Model(id: "secret-model", displayName: "SecretName"),
          Decision(), Prompts(revealIdentity: false), provider, reasoning, usage,
          "game-1", s => WindOf(game, s), _ => "LEAKED-NAME");
      room.AddPlayer(agent);

      agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      var prompt = provider.LastPrompt;
      Assert.IsFalse(prompt.Contains("LEAKED-NAME"));
      Assert.IsFalse(prompt.Contains("SecretName"));
      Assert.IsFalse(prompt.Contains("secret-model"));
      Assert.IsTrue(prompt.Contains("Player 2"));
    }

    [TestMethod]
    public void TableTalkGate_ControlsIncomingAndOutgoingChat() {
      var game = DiscardTurnGame();
      var providerOff = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"my rationale\", \"chat\": \"trash talk\"}"));

      var roomOff = NewRoom();
      var observerOff = new ChatObserver(-200, roomOff);
      roomOff.AddPlayer(observerOff);
      var agentOff = BuildAgent(roomOff, game, providerOff, Prompts(enableTableTalk: false), out _, out _);

      agentOff.OnChat(senderId: -200, text: "opponent secret plan", sticker: null);
      var (gi, pi, _) = DiscardInquiry(game);
      agentOff.Run(gi, pi, TimeSpan.FromSeconds(30));

      Assert.IsFalse(providerOff.LastPrompt.Contains("opponent secret plan"));
      // Rationale still lands in replay observer even when table talk is disabled!
      CollectionAssert.Contains(observerOff.Texts, "my rationale");

      // Now with EnableTableTalk = true:
      var providerOn = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"quiet\", \"chat\": \"Riichi time!\"}"));
      var roomOn = NewRoom();
      var other = new DefaultAI(-102, roomOn, UserStatus.InRoom);
      roomOn.AddPlayer(other);
      var observerOn = new ChatObserver(-201, roomOn);
      roomOn.AddPlayer(observerOn);
      var agentOn = BuildAgent(roomOn, game, providerOn, Prompts(enableTableTalk: true), out _, out _);

      agentOn.OnChat(senderId: -102, text: "pushing hard", sticker: null);
      agentOn.Run(gi, pi, TimeSpan.FromSeconds(30));
      StringAssert.Contains(providerOn.LastPrompt, "pushing hard");
      CollectionAssert.Contains(observerOn.Texts, "Riichi time!");
    }

    [TestMethod]
    public void ClassifyError_MapsProviderErrorsToCategories() {
      Assert.AreEqual(UsageErrorCategory.Timeout, ArenaLlmDecider.ClassifyError("timeout"));
      Assert.AreEqual(UsageErrorCategory.Auth, ArenaLlmDecider.ClassifyError("HTTP 403 forbidden"));
      Assert.AreEqual(UsageErrorCategory.Auth, ArenaLlmDecider.ClassifyError("invalid api key"));
      Assert.AreEqual(UsageErrorCategory.RateLimited, ArenaLlmDecider.ClassifyError("HTTP 429"));
      Assert.AreEqual(UsageErrorCategory.InvalidResponse, ArenaLlmDecider.ClassifyError("empty response"));
      Assert.AreEqual(UsageErrorCategory.Network, ArenaLlmDecider.ClassifyError("connection refused"));
      Assert.AreEqual(UsageErrorCategory.Other, ArenaLlmDecider.ClassifyError(""));
    }

    private sealed class ChatObserver(int id, Room room)
        : AIAgent(id, room, UserStatus.InRoom) {
      public readonly List<string> Texts = new();
      public override AiType aiType => AiType.Dummy;
      public override void OnChat(int senderId, string text, string sticker) {
        if (!string.IsNullOrEmpty(text)) Texts.Add(text);
      }
      protected override InquiryResponse Decide(
          MultiPlayerInquiry gi, SinglePlayerInquiry pi, TimeSpan t) =>
          InquiryResponse.Default(Seat);
    }
  }
}
