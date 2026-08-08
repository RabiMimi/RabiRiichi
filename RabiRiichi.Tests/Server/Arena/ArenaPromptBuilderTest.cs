using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Arena.Agents;
using RabiRiichi.Core;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Tests.Scenario;
using System.Collections.Generic;
using TileSource = RabiRiichi.Generated.Core.TileSource;
using DiscardReason = RabiRiichi.Generated.Core.DiscardReason;

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
      // Mirror the Arena: build the menu WITHOUT per-discard tenpai/wait/han.
      var menu = LlmActionMenu.Build(pi, annotateTenpai: false);
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
    public void TurnMessage_IncludesMenuAndToolContext_NoPerDiscardEfficiency() {
      var game = DiscardGame();
      var (view, _, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind));

      var msg = builder.BuildTurnMessage(view, menu);

      StringAssert.Contains(msg, "Legal actions");
      StringAssert.Contains(msg, "Pre-computed analysis");
      // The per-discard efficiency block (shanten AND ukeire) is intentionally
      // gone — the model must judge tile efficiency itself.
      Assert.IsFalse(msg.Contains("Per-discard"), "per-discard block must be removed");
      Assert.IsFalse(msg.Contains("ukeire"), "per-discard ukeire must be removed");
      // The menu enumerates ids but does NOT annotate which discards reach tenpai.
      StringAssert.Contains(msg, "[0]");
      Assert.IsFalse(msg.Contains("-> TENPAI"), "menu must not leak per-discard tenpai/waits/han");
    }

    [TestMethod]
    public void TurnMessage_Anonymized_HasNoOpponentNames() {
      var game = DiscardGame();
      var (view, pi, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(
          new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind, _ => "LEAK"));

      var msg = builder.BuildTurnMessage(view, menu);

      Assert.IsFalse(msg.Contains("LEAK"));
      StringAssert.Contains(msg, "Player 2");
    }

    [TestMethod]
    public void TurnMessage_IncludesValidationErrorOnRetry() {
      var game = DiscardGame();
      var (view, pi, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind));

      var msg = builder.BuildTurnMessage(
          view, menu, validationError: "action id 99 is not in the legal menu");

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
      var msg = builder.BuildTurnMessage(view, menu, chats: chats);
      StringAssert.Contains(msg, "watch out for my hand");
    }

    // ----- Rivers / melds & called-tile handling (§ prompt fidelity) -------

    // Builds a GameTile owned by <paramref name="owner"/>; when <paramref name="from"/>
    // is set the tile carries discard info (i.e. it was claimed from that player).
    private static GameTile GT(
        string tile, Player owner, TileSource src, Player from = null, int trace = 0) {
      var gt = new GameTile(new Tile(tile), trace) { player = owner, source = src };
      if (from != null) {
        gt.discardInfo = new DiscardInfo(from, DiscardReason.Draw, trace, 1);
      }
      return gt;
    }

    [TestMethod]
    public void TurnMessage_CalledTile_ShownInMeldWithSource_NotInDiscarderRiver() {
      var game = DiscardGame();
      var p0 = game.GetPlayer(0); // the viewer ("you")
      var p2 = game.GetPlayer(2);

      // Seat 0 discarded 1z (still in river) and 3p (which seat 2 then ponned).
      p0.hand.discarded.Add(GT("1z", p0, TileSource.Discard, trace: 11));
      var claimed = GT("3p", p2, TileSource.Pon, from: p0, trace: 12); // called away
      p0.hand.discarded.Add(claimed);

      // Seat 2's pon of 3p: two from its own hand + the tile claimed from seat 0.
      p2.hand.called.Add(new Kou(new[] {
        GT("3p", p2, TileSource.Pon, trace: 13),
        GT("3p", p2, TileSource.Pon, trace: 14),
        claimed,
      }));

      var (view, _, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(
          new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind));
      var msg = builder.BuildTurnMessage(view, menu);

      // The meld is annotated with type + WHICH tile was claimed + who from.
      StringAssert.Contains(msg, "pon");
      StringAssert.Contains(msg, "3p from you");

      // The called-away 3p left seat 0's river; the plain 1z stays.
      var river = System.Array.Find(
          msg.Split('\n'), l => l.Contains("Your discards (river):"));
      Assert.IsNotNull(river);
      StringAssert.Contains(river, "1z");
      Assert.IsFalse(river.Contains("3p"), "claimed tile must leave the discarder's river");
    }

    [TestMethod]
    public void TurnMessage_Chii_NamesTheSpecificClaimedTile() {
      var game = DiscardGame();
      var p1 = game.GetPlayer(1); // South — the discarder
      var p3 = game.GetPlayer(3); // North — the caller

      // North chii'd the MIDDLE tile (5m) of a 4m5m6m run off South's discard.
      var claimed = GT("5m", p3, TileSource.Chii, from: p1, trace: 21);
      p1.hand.discarded.Add(claimed); // called away -> filtered from South's river
      p3.hand.called.Add(new Shun(new[] {
        GT("4m", p3, TileSource.Chii, trace: 22),
        claimed,
        GT("6m", p3, TileSource.Chii, trace: 23),
      }));

      var (view, _, menu) = DiscardSetup(game);
      var builder = new ArenaPromptBuilder(
          new ArenaSeatLabeler(false, 0, s => game.GetPlayer(s).Wind));
      var msg = builder.BuildTurnMessage(view, menu);

      // The exact claimed tile (5m, not just "456m") is identified with its source.
      StringAssert.Contains(msg, "chii");
      StringAssert.Contains(msg, "5m from South (Player 2)");
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
