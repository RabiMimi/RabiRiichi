using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Generated.Core;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;
using RabiRiichi.Tests.Scenario;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Server.Agents.Llm {
  /// <summary>
  /// A scripted <see cref="ILlmProvider"/>: returns queued results and records
  /// the prompts it was called with. No network.
  /// </summary>
  internal sealed class ScriptedLlmProvider : ILlmProvider {
    private readonly Queue<LlmResult> results;
    public readonly List<IReadOnlyList<LlmMessage>> Calls = new();

    public ScriptedLlmProvider(params LlmResult[] scripted) {
      results = new Queue<LlmResult>(scripted);
    }

    public LlmProvider Provider => LlmProvider.Openai;
    public int CallCount => Calls.Count;

    public string LastPrompt => Calls.Count == 0
        ? ""
        : string.Join("\n", Calls[^1].Select(m => m.Content));

    public Task<LlmResult> CompleteAsync(
        IReadOnlyList<LlmMessage> messages, int maxOutputTokens, CancellationToken ct) {
      Calls.Add(messages);
      return Task.FromResult(results.Count > 0
          ? results.Dequeue()
          : LlmResult.Fail("no more scripted results"));
    }
  }

  internal sealed class ScriptedFactory(ILlmProvider provider) : ILlmProviderFactory {
    public ILlmProvider Create(LlmSettings settings) => provider;
  }

  /// <summary>
  /// End-to-end tests for the LLM seat: the model's chosen action is the one
  /// that gets played, and anything unusable degrades to the rule-based
  /// reference move rather than to a random or defaulted action.
  /// </summary>
  [TestClass]
  public class LlmAITest {
    private ILlmProviderFactory savedFactory;

    [TestInitialize]
    public void Setup() => savedFactory = LlmRuntime.Factory;

    [TestCleanup]
    public void Cleanup() => LlmRuntime.Factory = savedFactory;

    // ----- Fixtures --------------------------------------------------------

    private static LlmSettings Settings() =>
        LlmSettings.FromProto(new LlmAiConfig {
          Provider = LlmProvider.Openai,
          ApiToken = "sk",
          Model = "m",
          Language = "en",
          DisplayName = "TestBot",
        }, out _);

    /// <summary>A 4-player game where seat 0 is on a normal discard turn.</summary>
    private static Game DiscardTurnGame() {
      var builder = new ScenarioBuilder();
      builder.WithPlayer(0, p => p.SetFreeTiles("123m456p789s1234s"));
      Game game = null;
      builder.Build(0).WithGame(g => game = g);
      var player = game.GetPlayer(0);
      player.hand.pendingTile = new GameTile(new Tile("7z"), 999) { player = player };
      return game;
    }

    private static (MultiPlayerInquiry gi, SinglePlayerInquiry pi, PlayTileAction play)
        DiscardInquiry(Game game) {
      var player = game.GetPlayer(0);
      var hand14 = new List<GameTile>(player.hand.freeTiles) { player.hand.pendingTile };
      var play = new PlayTileAction(player, hand14, player.hand.pendingTile);
      var pi = new SinglePlayerInquiry(0);
      pi.AddAction(play);
      pi.DisableSkip();
      return (new MultiPlayerInquiry(game), pi, play);
    }

    private LlmAI BuildAgent(ScriptedLlmProvider provider, out Room room) {
      LlmRuntime.Factory = new ScriptedFactory(provider);
      room = new Room(new Random(0), new GameConfig { playerCount = 4 });
      var agent = new LlmAI(-101, room, Settings(), UserStatus.InRoom);
      room.AddPlayer(agent);
      return agent;
    }

    /// <summary>
    /// Drives the agent through its public inquiry entry point and waits for the
    /// answer (the base class always responds on a background task).
    /// </summary>
    private static InquiryResponse Ask(
        LlmAI agent, MultiPlayerInquiry gi, SinglePlayerInquiry pi) {
      var completion = new TaskCompletionSource<InquiryResponse>();
      agent.OnInquiry(gi, pi, TimeSpan.FromSeconds(30),
          resp => completion.TrySetResult(resp));
      Assert.IsTrue(completion.Task.Wait(TimeSpan.FromSeconds(30)),
          "the agent never answered the inquiry");
      return completion.Task.Result;
    }

    private static InquiryResponse ReferenceMove(Game game, SinglePlayerInquiry pi) =>
        RuleBasedStrategy.Decide(new PublicGameView(game, 0), pi);

    // ----- Tests -----------------------------------------------------------

    [TestMethod]
    public void TheModelsChosenActionIsThePlayedAction() {
      var game = DiscardTurnGame();
      var (gi, pi, play) = DiscardInquiry(game);
      var menu = LlmActionMenu.Build(pi);
      // Pick a legal option that is deliberately NOT what the rule-based
      // strategy would do, so the assertion proves the model actually decided.
      var reference = LlmActionMenu.Find(menu, ReferenceMove(game, pi));
      var chosen = menu.First(c => c.Id != reference.Id);

      var provider = new ScriptedLlmProvider(
          LlmResult.Ok($"{{\"action\": {chosen.Id}, \"say\": null, \"sticker\": null}}"));
      var agent = BuildAgent(provider, out _);

      var resp = Ask(agent, gi, pi);

      Assert.AreEqual(pi.actions.IndexOf(play), resp.index);
      Assert.AreEqual(chosen.SerializedResponse, resp.response);
      Assert.AreNotEqual(reference.SerializedResponse, resp.response);
      Assert.AreEqual(1, provider.CallCount);
    }

    [TestMethod]
    public void ThePromptOffersTheMenuAndTheReferenceMove() {
      var game = DiscardTurnGame();
      var (gi, pi, _) = DiscardInquiry(game);
      var provider = new ScriptedLlmProvider(LlmResult.Ok("{\"action\": 0}"));
      var agent = BuildAgent(provider, out _);

      Ask(agent, gi, pi);

      StringAssert.Contains(provider.LastPrompt, "Your legal actions this turn");
      StringAssert.Contains(provider.LastPrompt, "Reference move: [");
      StringAssert.Contains(provider.LastPrompt, "<-- reference move");
      StringAssert.Contains(provider.LastPrompt, "If you are unsure, follow the reference move");
    }

    [TestMethod]
    public void AnOutOfRangeActionIdFallsBackToTheReferenceMove() {
      var game = DiscardTurnGame();
      var (gi, pi, _) = DiscardInquiry(game);
      var provider = new ScriptedLlmProvider(LlmResult.Ok("{\"action\": 99}"));
      var agent = BuildAgent(provider, out _);

      var resp = Ask(agent, gi, pi);

      var reference = ReferenceMove(game, pi);
      Assert.AreEqual(reference.index, resp.index);
      Assert.AreEqual(reference.response, resp.response);
    }

    [TestMethod]
    public void AResponseWithNoActionFallsBackToTheReferenceMove() {
      var game = DiscardTurnGame();
      var (gi, pi, _) = DiscardInquiry(game);
      var provider = new ScriptedLlmProvider(
          LlmResult.Ok("{\"say\": \"thinking...\", \"sticker\": null}"));
      var agent = BuildAgent(provider, out _);

      var resp = Ask(agent, gi, pi);

      var reference = ReferenceMove(game, pi);
      Assert.AreEqual(reference.index, resp.index);
      Assert.AreEqual(reference.response, resp.response);
    }

    [TestMethod]
    public void AProviderFailureFallsBackToTheReferenceMove() {
      var game = DiscardTurnGame();
      var (gi, pi, _) = DiscardInquiry(game);
      var provider = new ScriptedLlmProvider(LlmResult.Fail("boom"));
      var agent = BuildAgent(provider, out _);

      var resp = Ask(agent, gi, pi);

      var reference = ReferenceMove(game, pi);
      Assert.AreEqual(reference.index, resp.index);
      Assert.AreEqual(reference.response, resp.response);
    }

    [TestMethod]
    public void GarbageOutputFallsBackToTheReferenceMove() {
      var game = DiscardTurnGame();
      var (gi, pi, _) = DiscardInquiry(game);
      var provider = new ScriptedLlmProvider(
          LlmResult.Ok("I think I will discard the seven of honors, thanks!"));
      var agent = BuildAgent(provider, out _);

      var resp = Ask(agent, gi, pi);

      var reference = ReferenceMove(game, pi);
      Assert.AreEqual(reference.index, resp.index);
      Assert.AreEqual(reference.response, resp.response);
    }

    [TestMethod]
    public void OutOfTurnCallsAreDecidedByTheModelToo() {
      // Reacting to somebody's discard used to bypass the model entirely. Now it
      // is a real decision: the model may pon even when the reference says pass.
      var game = DiscardTurnGame();
      var player = game.GetPlayer(0);
      player.hand.pendingTile = null;
      var own = player.hand.freeTiles.Where(t => t.tile.IsSame(new Tile("1s"))).ToList();
      Assert.AreEqual(1, own.Count);
      var claimed = new GameTile(new Tile("1s"), 500) {
        discardInfo = new DiscardInfo(game.GetPlayer(1), DiscardReason.Draw, 1),
      };
      var second = new GameTile(new Tile("1s"), 501);
      var pon = new PonAction(0, new List<List<GameTile>> {
        new() { own[0], second, claimed },
      });
      var pi = new SinglePlayerInquiry(0);
      pi.AddAction(pon);
      var gi = new MultiPlayerInquiry(game);

      var menu = LlmActionMenu.Build(pi);
      var ponChoice = menu.First(c => c.Kind == "pon");
      var provider = new ScriptedLlmProvider(
          LlmResult.Ok($"{{\"action\": {ponChoice.Id}}}"));
      var agent = BuildAgent(provider, out _);

      var resp = Ask(agent, gi, pi);

      Assert.AreEqual(1, provider.CallCount, "the model must be consulted");
      StringAssert.Contains(provider.LastPrompt, "out-of-turn reaction");
      Assert.AreEqual(pi.actions.IndexOf(pon), resp.index);
    }

    /// <summary> Runs <paramref name="turns"/> identical discard turns. </summary>
    private List<IReadOnlyList<LlmMessage>> PlayTurns(LlmAI agent, Game game, int turns) {
      for (int turn = 0; turn < turns; turn++) {
        var (gi, pi, _) = DiscardInquiry(game);
        Ask(agent, gi, pi);
      }
      return null;
    }

    private static ScriptedLlmProvider AlwaysAnswers(int turns) =>
        new(Enumerable.Range(0, turns + 5)
            .Select(_ => LlmResult.Ok("{\"action\":0,\"say\":null,\"sticker\":null}"))
            .ToArray());

    [TestMethod]
    [Timeout(120_000)]
    public void EachRequestExtendsThePreviousOneSoItCanBeServedFromCache() {
      // A stateless provider bills the newest turn at full rate and the repeated
      // prefix at a discount, but only on an EXACT prefix match. So the request
      // must be a strict extension of the last one: trimming the transcript by a
      // little every turn would move the prefix and forfeit the whole discount.
      var game = DiscardTurnGame();
      var provider = AlwaysAnswers(20);
      var agent = BuildAgent(provider, out _);
      PlayTurns(agent, game, 20);

      for (int i = 1; i < provider.Calls.Count; i++) {
        var prev = provider.Calls[i - 1];
        var cur = provider.Calls[i];
        // Everything of the previous request except its trailing turn prompt
        // must reappear, unchanged and in order, at the head of this one.
        for (int m = 0; m < prev.Count - 1; m++) {
          Assert.AreEqual(prev[m].Content, cur[m].Content,
              $"request {i} diverges from request {i - 1} at message {m}");
        }
      }
    }

    [TestMethod]
    [Timeout(120_000)]
    public void CondensingSupersededTurnsKeepsHistoryGrowthSmall() {
      // History is kept, but a superseded turn is stored as a stub rather than
      // the full ~2800-character position it was.
      var game = DiscardTurnGame();
      var provider = AlwaysAnswers(25);
      var agent = BuildAgent(provider, out _);
      PlayTurns(agent, game, 25);

      var sizes = provider.Calls.Select(c => c.Sum(m => m.Content.Length)).ToList();
      double perTurn = (sizes[^1] - sizes[4]) / 20.0;
      Assert.IsTrue(perTurn < 400,
          $"history grew {perTurn:0} chars/turn; storing full prompts would be ~2800");
    }

    [TestMethod]
    [Timeout(300_000)]
    public void AVeryLongMatchStillCannotOverrunTheContextWindow() {
      // The bound is a rare block cut, not a per-turn slide, so it must actually
      // fire and cap the transcript once a match runs long enough.
      int turns = LlmLimits.TranscriptMaxExchanges + 8;
      var game = DiscardTurnGame();
      var provider = AlwaysAnswers(turns);
      var agent = BuildAgent(provider, out _);
      PlayTurns(agent, game, turns);

      int cap = LlmLimits.TranscriptMaxExchanges * 2 + 2; // + system + current turn
      int biggest = provider.Calls.Max(c => c.Count);
      Assert.IsTrue(biggest <= cap,
          $"transcript reached {biggest} messages, cap is {cap}");
      // The cut is a block, so the transcript must visibly shrink at some point
      // rather than creeping along at the cap.
      Assert.IsTrue(provider.Calls[^1].Count < biggest,
          "the block trim should have cut the transcript back");
    }

    [TestMethod]
    public void HistoryKeepsTheModelsRepliesButNotStaleBoardStates() {
      var game = DiscardTurnGame();
      var provider = new ScriptedLlmProvider(
          Enumerable.Range(0, 10)
              .Select(i => LlmResult.Ok(
                  $"{{\"action\":0,\"say\":\"line {i}\",\"sticker\":null}}"))
              .ToArray());
      var agent = BuildAgent(provider, out _);

      for (int turn = 0; turn < 4; turn++) {
        var (gi, pi, _) = DiscardInquiry(game);
        Ask(agent, gi, pi);
      }

      var last = provider.Calls[^1];
      // What the model said is preserved verbatim...
      Assert.IsTrue(last.Any(m => m.Content.Contains("\"say\":\"line 2\"")),
          "the model's own replies must stay in the transcript");
      // ...but each superseded position is replaced by a stub.
      Assert.IsTrue(last.Any(m => m.Content.Contains("The position from that turn is omitted")));
      int menus = last.Count(m => m.Content.Contains("Your legal actions this turn"));
      Assert.AreEqual(1, menus,
          "only the current turn should carry a full position and action list");
    }

    [TestMethod]
    public void AForcedActionStillAsksForChatButNotForADecision() {
      // Only one legal action: nothing to choose, so the model is told what
      // happened and answers with the chat-only framing.
      var game = DiscardTurnGame();
      var player = game.GetPlayer(0);
      var pi = new SinglePlayerInquiry(0);
      pi.AddAction(new PlayTileAction(
          player, [player.hand.pendingTile], player.hand.pendingTile));
      pi.DisableSkip();
      var gi = new MultiPlayerInquiry(game);

      var provider = new ScriptedLlmProvider(LlmResult.Ok("{\"say\": \"tsumogiri~\"}"));
      var agent = BuildAgent(provider, out _);

      var resp = Ask(agent, gi, pi);

      Assert.AreEqual(1, provider.CallCount);
      StringAssert.Contains(provider.LastPrompt, "You decided to:");
      Assert.IsFalse(provider.LastPrompt.Contains("Your legal actions this turn"));
      Assert.AreEqual(0, resp.index);
    }
  }
}
