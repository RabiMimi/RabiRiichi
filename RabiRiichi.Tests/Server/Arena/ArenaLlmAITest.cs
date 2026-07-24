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
  /// <summary>
  /// A scripted <see cref="ILlmProvider"/> that returns a queued sequence of
  /// results and records the messages it was called with. No network.
  /// </summary>
  internal sealed class FakeLlmProvider : ILlmProvider {
    private readonly Queue<LlmResult> results;
    public readonly List<IReadOnlyList<LlmMessage>> Calls = new();

    public FakeLlmProvider(params LlmResult[] scripted) {
      results = new Queue<LlmResult>(scripted);
    }

    public ProtoLlmProvider Provider => ProtoLlmProvider.Openai;

    public int CallCount => Calls.Count;

    /// <summary>The concatenated text of the last call's messages.</summary>
    public string LastPrompt => Calls.Count == 0
        ? ""
        : string.Join("\n", Calls[^1].Select(m => m.Content));

    public Task<LlmResult> CompleteAsync(
        IReadOnlyList<LlmMessage> messages, int maxOutputTokens, CancellationToken ct) {
      Calls.Add(messages);
      var r = results.Count > 0 ? results.Dequeue() : LlmResult.Fail("no more scripted results");
      return Task.FromResult(r);
    }
  }

  /// <summary>Test seam exposing the protected <see cref="ArenaLlmAI.Decide"/>.</summary>
  internal sealed class TestableArenaLlmAI : ArenaLlmAI {
    public TestableArenaLlmAI(
        int id, Room room, ArenaConfig.ModelConfig model,
        ArenaConfig.DecisionConfig decision, ArenaConfig.ExposureConfig exposure,
        ILlmProvider provider, ReasoningStore reasoning, UsageStats usage,
        string gameId, Func<int, Wind> windOf, Func<int, string> displayNameOf = null)
        : base(id, room, model, decision, exposure, provider, reasoning, usage,
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

    // ----- Fixtures --------------------------------------------------------

    private static ArenaConfig.ModelConfig Model(
        string id = "gpt-x", string displayName = "GPT-X") => new() {
      Id = id,
      DisplayName = displayName,
      Provider = "openai",
      Model = "gpt-x-model",
    };

    private static ArenaConfig.DecisionConfig Decision(int maxRetries = 3) => new() {
      TimeoutSeconds = 60,
      MaxRetries = maxRetries,
      MaxOutputTokens = 256,
    };

    private static ArenaConfig.ExposureConfig Exposure(
        bool revealIdentity = false, bool chatToAgents = false) => new() {
      RevealOpponentIdentity = revealIdentity,
      ChatToAgents = chatToAgents,
    };

    /// <summary>A 4-player game where seat 0 has a normal discard turn.</summary>
    private static Game DiscardTurnGame() {
      // Configure only seat 0's concealed hand; the other seats are auto-filled
      // by the wall builder (matching the engine agent tests' convention).
      var builder = new ScenarioBuilder();
      builder.WithPlayer(0, p => p.SetFreeTiles("123m456p789s1234s"));
      Game game = null;
      builder.Build(0).WithGame(g => game = g);
      // Give seat 0 a freshly drawn tile so it is a 14-tile discard turn.
      var player = game.GetPlayer(0);
      var draw = new GameTile(new Tile("7z"), 999) { player = player };
      player.hand.pendingTile = draw;
      return game;
    }

    /// <summary>A discard inquiry for seat 0 over its 14 tiles.</summary>
    private static (MultiPlayerInquiry gi, SinglePlayerInquiry pi, PlayTileAction play)
        DiscardInquiry(Game game) {
      var player = game.GetPlayer(0);
      var hand14 = new List<GameTile>(player.hand.freeTiles) { player.hand.pendingTile };
      var play = new PlayTileAction(player, hand14, hand14[0]);
      var pi = new SinglePlayerInquiry(0);
      pi.AddAction(play);
      // Drop the implicit SkipAction so the menu is discards only; then menu id 0
      // is the first discard option (a real self-turn discard inquiry has no skip).
      pi.DisableSkip();
      var gi = new MultiPlayerInquiry(game);
      return (gi, pi, play);
    }

    private static Wind WindOf(Game game, int seat) => game.GetPlayer(seat).Wind;

    private TestableArenaLlmAI BuildAgent(
        Room room, Game game, FakeLlmProvider provider,
        ArenaConfig.ExposureConfig exposure, out ReasoningStore reasoning,
        out UsageStats usage, string modelId = "gpt-x", string gameId = "game-1",
        int maxRetries = 3, bool revealNames = false) {
      reasoning = new ReasoningStore(workspaceDir);
      usage = new UsageStats(workspaceDir);
      var agent = new TestableArenaLlmAI(
          -101, room, Model(modelId), Decision(maxRetries), exposure, provider,
          reasoning, usage, gameId, s => WindOf(game, s),
          revealNames ? _ => "GPT-X" : (Func<int, string>)null);
      room.AddPlayer(agent);
      return agent;
    }

    private static Room NewRoom() =>
        new(new Random(0), new GameConfig { playerCount = 4 });

    // ----- Tests -----------------------------------------------------------

    [TestMethod]
    public void ValidResponseFirstAttempt_ReturnedAndSuccessRecorded() {
      var game = DiscardTurnGame();
      var (gi, pi, play) = DiscardInquiry(game);
      var provider = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"cut the isolated honor\"}"));

      var room = NewRoom();
      var agent = BuildAgent(room, game, provider, Exposure(), out var reasoning, out var usage);

      var resp = agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      Assert.AreEqual(pi.actions.IndexOf(play), resp.index);
      var u = usage.Get("gpt-x");
      Assert.IsNotNull(u);
      Assert.AreEqual(1, u.Requests);
      Assert.AreEqual(1, u.Successes);
      Assert.AreEqual(0, u.Penalties);
      Assert.AreEqual(0, u.Retries);
      Assert.AreEqual(1, provider.CallCount);

      var turns = reasoning.ReadTurns("game-1", 0);
      Assert.AreEqual(1, turns.Count);
      Assert.IsTrue(turns[0].Valid);
      Assert.IsFalse(turns[0].Penalized);
      Assert.AreEqual(1, turns[0].Attempts);
      Assert.AreEqual("cut the isolated honor", turns[0].Rationale);
    }

    [TestMethod]
    public void InvalidThreeTimes_ReturnsDefaultAndPenalizes() {
      var game = DiscardTurnGame();
      var (gi, pi, _) = DiscardInquiry(game);
      // Every answer picks an out-of-range id -> always invalid.
      var provider = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 99, \"rationale\": \"a\"}"),
          LlmResult.Ok("{\"action\": 88, \"rationale\": \"b\"}"),
          LlmResult.Ok("{\"action\": 77, \"rationale\": \"c\"}"));

      var room = NewRoom();
      var agent = BuildAgent(room, game, provider, Exposure(), out var reasoning, out var usage);

      var resp = agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      // Penalty falls back to the engine default (index -1).
      Assert.AreEqual(-1, resp.index);
      Assert.AreEqual(3, provider.CallCount);

      var u = usage.Get("gpt-x");
      Assert.AreEqual(3, u.Requests);
      Assert.AreEqual(1, u.Penalties);
      Assert.AreEqual(2, u.Retries); // attempts 2 and 3 are retries

      var turns = reasoning.ReadTurns("game-1", 0);
      Assert.AreEqual(1, turns.Count);
      Assert.IsFalse(turns[0].Valid);
      Assert.IsTrue(turns[0].Penalized);
      Assert.AreEqual(3, turns[0].Attempts);
      Assert.IsNotNull(turns[0].Error);
    }

    [TestMethod]
    public void InvalidThenValid_ReturnedWithRetryCounted() {
      var game = DiscardTurnGame();
      var (gi, pi, play) = DiscardInquiry(game);
      var provider = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 99, \"rationale\": \"bad\"}"),
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"good\"}"));

      var room = NewRoom();
      var agent = BuildAgent(room, game, provider, Exposure(), out var reasoning, out var usage);

      var resp = agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      Assert.AreEqual(pi.actions.IndexOf(play), resp.index);
      Assert.AreEqual(2, provider.CallCount);

      var u = usage.Get("gpt-x");
      Assert.AreEqual(2, u.Requests);
      Assert.AreEqual(2, u.Successes); // both transport calls succeeded
      Assert.AreEqual(0, u.Penalties);
      Assert.AreEqual(1, u.Retries);

      var turns = reasoning.ReadTurns("game-1", 0);
      Assert.AreEqual(1, turns.Count);
      Assert.IsTrue(turns[0].Valid);
      Assert.AreEqual(2, turns[0].Attempts);

      // The retry prompt must echo the validation error so the model can correct.
      Assert.IsTrue(provider.Calls[^1].Any(m => m.Content.Contains("REJECTED")));
    }

    [TestMethod]
    public void ReasoningArtifacts_MetaOnceAndPromptDeltaHasNoPriorTranscript() {
      var game = DiscardTurnGame();
      var provider = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"turn one\"}"),
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"turn two\"}"));

      var room = NewRoom();
      var agent = BuildAgent(room, game, provider, Exposure(), out var reasoning, out var usage);

      // Two decisions in a row.
      var (gi1, pi1, _) = DiscardInquiry(game);
      agent.Run(gi1, pi1, TimeSpan.FromSeconds(30));
      var (gi2, pi2, _) = DiscardInquiry(game);
      agent.Run(gi2, pi2, TimeSpan.FromSeconds(30));

      // Meta written exactly once.
      var meta = reasoning.ReadMeta("game-1", 0);
      Assert.IsNotNull(meta);
      Assert.AreEqual("gpt-x", meta.ModelId);
      Assert.IsFalse(string.IsNullOrEmpty(meta.SystemPrompt));

      var turns = reasoning.ReadTurns("game-1", 0);
      Assert.AreEqual(2, turns.Count);

      // The second turn's promptDelta must NOT contain the first turn's model
      // reply ("turn one") — i.e. no accumulated transcript is duplicated.
      Assert.IsFalse(turns[1].PromptDelta.Contains("turn one"),
          "promptDelta must be only this turn's new message(s), not the history");
      // Nor should the system prompt be duplicated into the delta.
      Assert.IsFalse(turns[1].PromptDelta.Contains(meta.SystemPrompt));
    }

    [TestMethod]
    public void Anonymized_PromptHasNoOpponentIdentityAndUsesSeatLabels() {
      var game = DiscardTurnGame();
      var (gi, pi, _) = DiscardInquiry(game);
      var provider = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"x\"}"));

      var room = NewRoom();
      // Identity NOT revealed. displayNameOf resolver returns a leak string, but
      // must never be consulted under anonymity.
      var reasoning = new ReasoningStore(workspaceDir);
      var usage = new UsageStats(workspaceDir);
      var agent = new TestableArenaLlmAI(
          -101, room, Model(id: "secret-model", displayName: "SecretName"),
          Decision(), Exposure(revealIdentity: false), provider, reasoning, usage,
          "game-1", s => WindOf(game, s), _ => "LEAKED-NAME");
      room.AddPlayer(agent);

      agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      var prompt = provider.LastPrompt;
      Assert.IsFalse(prompt.Contains("LEAKED-NAME"), "display name must not leak");
      Assert.IsFalse(prompt.Contains("SecretName"), "roster display name must not leak");
      Assert.IsFalse(prompt.Contains("secret-model"), "model id must not leak");
      // Opponents referenced by neutral seat label.
      Assert.IsTrue(prompt.Contains("Player 2") || prompt.Contains("Player 3")
          || prompt.Contains("Player 4"), "opponents should use neutral seat labels");
    }

    [TestMethod]
    public void RevealedIdentity_PromptMayContainDisplayName() {
      var game = DiscardTurnGame();
      var (gi, pi, _) = DiscardInquiry(game);
      var provider = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"x\"}"));

      var room = NewRoom();
      var reasoning = new ReasoningStore(workspaceDir);
      var usage = new UsageStats(workspaceDir);
      // Reveal identities; opponents' names resolve to "Rival-N".
      var agent = new TestableArenaLlmAI(
          -101, room, Model(), Decision(), Exposure(revealIdentity: true),
          provider, reasoning, usage, "game-1", s => WindOf(game, s),
          s => $"Rival-{s}");
      room.AddPlayer(agent);

      agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      var prompt = provider.LastPrompt;
      Assert.IsTrue(prompt.Contains("Rival-"), "revealed opponent names should appear");
    }

    [TestMethod]
    public void ChatToAgentsOff_IncomingChatDoesNotAffectNextPrompt() {
      var game = DiscardTurnGame();
      var provider = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"x\"}"));

      var room = NewRoom();
      var agent = BuildAgent(room, game, provider, Exposure(chatToAgents: false),
          out _, out _);

      // Simulate an opponent's chat arriving before the decision.
      agent.OnChat(senderId: -102, text: "opponent secret plan", sticker: null);

      var (gi, pi, _) = DiscardInquiry(game);
      agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      Assert.IsFalse(provider.LastPrompt.Contains("opponent secret plan"),
          "with chatToAgents off, incoming chat must not enter the prompt");
    }

    [TestMethod]
    public void ChatToAgentsOn_IncomingChatAppearsInNextPrompt() {
      var game = DiscardTurnGame();
      var provider = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"x\"}"));

      var room = NewRoom();
      // Add a second agent so the sender id maps to a real seat (for labeling).
      var other = new DefaultAI(-102, room, UserStatus.InRoom);
      room.AddPlayer(other);
      var agent = BuildAgent(room, game, provider, Exposure(chatToAgents: true),
          out _, out _);

      agent.OnChat(senderId: -102, text: "I am pushing hard", sticker: null);

      var (gi, pi, _) = DiscardInquiry(game);
      agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      Assert.IsTrue(provider.LastPrompt.Contains("I am pushing hard"),
          "with chatToAgents on, incoming chat should appear in the prompt");
    }

    [TestMethod]
    public void ProviderError_MappedToCorrectCategoryAndPenalizes() {
      var game = DiscardTurnGame();
      var (gi, pi, _) = DiscardInquiry(game);
      // All three attempts fail at the transport with an auth error.
      var provider = new FakeLlmProvider(
          LlmResult.Fail("HTTP 401: invalid api key"),
          LlmResult.Fail("HTTP 401: invalid api key"),
          LlmResult.Fail("HTTP 401: invalid api key"));

      var room = NewRoom();
      var agent = BuildAgent(room, game, provider, Exposure(), out _, out var usage);

      var resp = agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      Assert.AreEqual(-1, resp.index); // penalized -> default
      var u = usage.Get("gpt-x");
      Assert.AreEqual(3, u.Failures);
      Assert.AreEqual(3, u.AuthErrors);
      Assert.AreEqual(0, u.Successes);
      Assert.AreEqual(1, u.Penalties);
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

    [TestMethod]
    public void RationaleBroadcastToReplayRegardlessOfChatGate() {
      var game = DiscardTurnGame();
      var provider = new FakeLlmProvider(
          LlmResult.Ok("{\"action\": 0, \"rationale\": \"my reasoning line\"}"));

      var room = NewRoom();
      // An observer agent records chat it receives via the room fan-out.
      var observer = new ChatObserver(-200, room);
      room.AddPlayer(observer);
      var agent = BuildAgent(room, game, provider, Exposure(chatToAgents: false),
          out _, out _);

      var (gi, pi, _) = DiscardInquiry(game);
      agent.Run(gi, pi, TimeSpan.FromSeconds(30));

      // Even with chatToAgents off, the rationale is broadcast (lands in replay).
      CollectionAssert.Contains(observer.Texts, "my reasoning line");
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
