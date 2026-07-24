using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Arena.Agents;
using RabiRiichi.Core;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Tests.Scenario;
using System.Collections.Generic;

namespace RabiRiichi.Tests.Server.Arena {
  /// <summary>
  /// Unit tests for the pure prompt/tool-context builder, seat labeler, and
  /// response parser — the pieces the design wants directly tested (anonymity,
  /// tool context, parsing).
  /// </summary>
  [TestClass]
  public class ArenaPromptBuilderTest {
    private static Game DiscardGame() {
      var builder = new ScenarioBuilder();
      builder.WithPlayer(0, p => p.SetFreeTiles("123m456p789s1234s"));
      Game game = null;
      builder.Build(0).WithGame(g => game = g);
      var player = game.GetPlayer(0);
      player.hand.pendingTile = new GameTile(new Tile("7z"), 999) { player = player };
      return game;
    }

    private static (PublicGameView view, SinglePlayerInquiry pi, IReadOnlyList<LlmChoice> menu)
        DiscardSetup(Game game) {
      var player = game.GetPlayer(0);
      var hand14 = new List<GameTile>(player.hand.freeTiles) { player.hand.pendingTile };
      var play = new PlayTileAction(player, hand14, hand14[0]);
      var pi = new SinglePlayerInquiry(0);
      pi.AddAction(play);
      pi.DisableSkip();
      var menu = LlmActionMenu.Build(pi);
      return (new PublicGameView(game, 0), pi, menu);
    }

    // ----- ArenaSeatLabeler ------------------------------------------------

    [TestMethod]
    public void Labeler_Anonymized_UsesNeutralSeatLabelsOnly() {
      var labeler = new ArenaSeatLabeler(
          revealIdentity: false, selfSeat: 0,
          windOf: s => (Wind)s, displayNameOf: _ => "REAL-NAME");
      Assert.AreEqual("You", labeler.Label(0));
      // Opponent labels never contain the real name.
      var l1 = labeler.Label(1);
      StringAssert.Contains(l1, "Player 2");
      Assert.IsFalse(l1.Contains("REAL-NAME"));
    }

    [TestMethod]
    public void Labeler_Revealed_AppendsDisplayName() {
      var labeler = new ArenaSeatLabeler(
          revealIdentity: true, selfSeat: 0,
          windOf: s => (Wind)s, displayNameOf: s => $"Rival-{s}");
      StringAssert.Contains(labeler.Label(1), "Rival-1");
    }

    [TestMethod]
    public void Labeler_Revealed_FallsBackToNeutralWhenNoName() {
      var labeler = new ArenaSeatLabeler(
          revealIdentity: true, selfSeat: 0,
          windOf: s => (Wind)s, displayNameOf: _ => "");
      StringAssert.Contains(labeler.Label(1), "Player 2");
    }

    // ----- ArenaPromptBuilder ---------------------------------------------

    [TestMethod]
    public void SystemPrompt_DescribesJsonContractAndCarriesNoOpponentInfo() {
      var labeler = new ArenaSeatLabeler(false, 0, s => (Wind)s);
      var builder = new ArenaPromptBuilder(labeler);
      var (view, _, _) = DiscardSetup(DiscardGame());
      var sys = builder.BuildSystemPrompt(2, view);
      StringAssert.Contains(sys, "action");
      StringAssert.Contains(sys, "rationale");
      // Loaded from the embedded .md template and fully substituted.
      StringAssert.Contains(sys, "seat 2");
      StringAssert.Contains(sys, "manzu");
      Assert.IsFalse(sys.Contains("{{"), "template placeholders must be substituted");
    }

    [TestMethod]
    public void SystemPrompt_IncludesHumanVisibleGameConfig() {
      var labeler = new ArenaSeatLabeler(false, 0, s => (Wind)s);
      var builder = new ArenaPromptBuilder(labeler);
      var (view, _, _) = DiscardSetup(DiscardGame());
      var sys = builder.BuildSystemPrompt(0, view);
      // The same config a human sees in the in-game info modal (§ config parity):
      // length, min han, and the enabled yaku list must be present.
      StringAssert.Contains(sys, "Length:");
      StringAssert.Contains(sys, "Minimum han");
      StringAssert.Contains(sys, "Enabled yaku");
    }

    [TestMethod]
    public void TurnMessage_IncludesMenuAndPerDiscardToolContext() {
      var game = DiscardGame();
      var (view, pi, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind));

      var msg = builder.BuildTurnMessage(view, menu, pi);

      StringAssert.Contains(msg, "Legal actions");
      StringAssert.Contains(msg, "Pre-computed analysis");
      // Per-discard analysis lists shanten/ukeire for a candidate discard.
      StringAssert.Contains(msg, "ukeire");
      // The menu enumerates ids the model must pick from.
      StringAssert.Contains(msg, "[0]");
    }

    [TestMethod]
    public void TurnMessage_Anonymized_HasNoOpponentNames() {
      var game = DiscardGame();
      var (view, pi, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(
          new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind, _ => "LEAK"));

      var msg = builder.BuildTurnMessage(view, menu, pi);

      Assert.IsFalse(msg.Contains("LEAK"));
      StringAssert.Contains(msg, "Player 2");
    }

    [TestMethod]
    public void TurnMessage_IncludesValidationErrorOnRetry() {
      var game = DiscardGame();
      var (view, pi, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind));

      var msg = builder.BuildTurnMessage(
          view, menu, pi, validationError: "action id 99 is not in the legal menu");

      StringAssert.Contains(msg, "REJECTED");
      StringAssert.Contains(msg, "99");
    }

    [TestMethod]
    public void TurnMessage_FoldsInChatWhenProvided() {
      var game = DiscardGame();
      var (view, pi, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind));

      var chats = new List<ArenaChatLine> {
        new("South (Player 2)", "watch out for my hand"),
      };
      var msg = builder.BuildTurnMessage(view, menu, pi, chats: chats);
      StringAssert.Contains(msg, "watch out for my hand");
    }

    // ----- ArenaLlmResponse ------------------------------------------------

    [TestMethod]
    public void Response_ParsesActionAndRationale() {
      var r = ArenaLlmResponse.Parse("{\"action\": 3, \"rationale\": \"safe tile\"}");
      Assert.IsTrue(r.HasAction);
      Assert.AreEqual(3, r.ActionId);
      Assert.AreEqual("safe tile", r.Rationale);
    }

    [TestMethod]
    public void Response_ToleratesCodeFencesAndProse() {
      var raw = "Here is my move:\n```json\n{\"action\": 2, \"rationale\": \"push\"}\n```";
      var r = ArenaLlmResponse.Parse(raw);
      Assert.AreEqual(2, r.ActionId);
      Assert.AreEqual("push", r.Rationale);
    }

    [TestMethod]
    public void Response_AcceptsNumericStringAction() {
      var r = ArenaLlmResponse.Parse("{\"action\": \"5\", \"rationale\": \"ok\"}");
      Assert.AreEqual(5, r.ActionId);
    }

    [TestMethod]
    public void Response_MissingActionHasNoAction() {
      var r = ArenaLlmResponse.Parse("{\"rationale\": \"thinking...\"}");
      Assert.IsFalse(r.HasAction);
      Assert.AreEqual("thinking...", r.Rationale);
    }

    [TestMethod]
    public void Response_GarbageYieldsEmpty() {
      var r = ArenaLlmResponse.Parse("no json here at all");
      Assert.IsFalse(r.HasAction);
      Assert.IsNull(r.Rationale);
    }
  }
}
