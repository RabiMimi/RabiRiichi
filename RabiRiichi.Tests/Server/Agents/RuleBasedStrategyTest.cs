using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Events.InGame;
using RabiRiichi.Patterns;
using RabiRiichi.Server.Agents;
using RabiRiichi.Tests.Scenario;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace RabiRiichi.Tests.Server.Agents {
  [TestClass]
  public class RuleBasedStrategyTest {
    /// <summary> Builds a 4-player game and returns the raw Game object. </summary>
    private static Game BuildGame(System.Action<ScenarioBuilder> configure) {
      var builder = new ScenarioBuilder();
      configure(builder);
      Game game = null;
      builder.Build(0).WithGame(g => game = g);
      return game;
    }

    private static PublicGameView ViewFor(Game game, int seat) => new(game, seat);

    #region Anti-cheat: PublicGameView restricts to fair information

    [TestMethod]
    public void PublicView_CountsOwnHandButNotOpponentConcealedTiles() {
      // Seat 0 holds two 1m; seat 1 (opponent) also holds 1m concealed.
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("11m23456789p123s"))
          .WithPlayer(1, p => p.SetFreeTiles("1m23456789p1234s")));
      var view = ViewFor(game, 0);

      // Only the two 1m in seat 0's own hand are visible; seat 1's concealed 1m
      // must NOT be counted (that would be cheating).
      Assert.AreEqual(2, view.VisibleCount(new Tile("1m")));
      Assert.AreEqual(2, view.UnseenCount(new Tile("1m")));
    }

    [TestMethod]
    public void PublicView_CountsOpponentDiscardsAndMelds() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("23456789p12345s"))
          .WithPlayer(1, p => p
              .SetFreeTiles("234567p234567s1z")
              .SetDiscarded(1, "1m")));
      var view = ViewFor(game, 0);

      // The opponent's discarded 1m is public and must be counted.
      Assert.AreEqual(1, view.VisibleCount(new Tile("1m")));
    }

    #endregion

    #region Discard safety

    [TestMethod]
    public void Safety_GenbutsuIsSaferThanLiveCentralTile() {
      // Seat 1 is in riichi and has discarded 3p (genbutsu against them).
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p"))
          .WithPlayer(1, p => p
              .SetFreeTiles("123456789p1234s")
              .SetDiscarded(2, "3p")
              .SetRiichiTile("9s")));
      var view = ViewFor(game, 0);

      double genbutsu = RuleBasedStrategy.SafetyScore(view, new Tile("3p"));
      double liveCentral = RuleBasedStrategy.SafetyScore(view, new Tile("5m"));
      Assert.IsTrue(genbutsu > liveCentral,
          $"Genbutsu safety {genbutsu} should exceed live central {liveCentral}");
    }

    [TestMethod]
    public void Safety_ZeroWhenNobodyIsRiichi() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p")));
      var view = ViewFor(game, 0);
      Assert.AreEqual(0, RuleBasedStrategy.SafetyScore(view, new Tile("5m")));
    }

    #endregion

    #region Inquiry decisions

    private static SinglePlayerInquiry InquiryWith(int seat, params IPlayerAction[] actions) {
      var inquiry = new SinglePlayerInquiry(seat); // adds a SkipAction as default
      foreach (var action in actions) {
        inquiry.AddAction(action);
      }
      return inquiry;
    }

    [TestMethod]
    public void Decide_AlwaysTakesAWin() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p")));
      var view = ViewFor(game, 0);

      var winTile = new GameTile(new Tile("1p"), 1);
      var scores = new ScoreStorage();
      var ron = new RonAction(0, scores, winTile);
      var inquiry = InquiryWith(0, ron);

      var resp = RuleBasedStrategy.Decide(view, inquiry);
      Assert.AreEqual(inquiry.actions.IndexOf(ron), resp.index);
      Assert.AreEqual("{}", resp.response);
    }

    [TestMethod]
    public void Decide_SkipsWhenOnlySkipIsOffered() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1234p")));
      var view = ViewFor(game, 0);

      // Only the implicit SkipAction is present.
      var inquiry = new SinglePlayerInquiry(0);
      var resp = RuleBasedStrategy.Decide(view, inquiry);
      // Default response declines (index -1 -> engine keeps the Skip default).
      Assert.AreEqual(-1, resp.index);
    }

    [TestMethod]
    public void Decide_DoesNotCallChiiToStayConcealed() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("13m2345678p1234s")));
      var view = ViewFor(game, 0);

      // Offer a chii of 1m-2m-3m (a non-yakuhai call): the AI should decline.
      var group = new List<GameTile> {
        new(new Tile("1m"), 1),
        new(new Tile("2m"), 2),
        new(new Tile("3m"), 3),
      };
      var chii = new ChiiAction(0, new List<List<GameTile>> { group });
      var inquiry = InquiryWith(0, chii);

      var resp = RuleBasedStrategy.Decide(view, inquiry);
      // Declines the chii -> not the chii action index.
      Assert.AreNotEqual(inquiry.actions.IndexOf(chii), resp.index);
    }

    #endregion

    #region Shanten reduction (efficiency)

    /// <summary>
    /// Sets up a realistic discard turn: a 13-tile concealed hand plus a freshly
    /// drawn pending tile, and returns the 14 discardable tiles plus a
    /// PlayTileAction built over them (with server-computed candidates).
    /// </summary>
    private static (Game game, PublicGameView view, List<GameTile> hand14, PlayTileAction action)
        DiscardTurn(string freeTiles13, string drawn) {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles(freeTiles13)));
      var player = game.GetPlayer(0);

      // Simulate drawing a tile: it becomes the pending tile, giving 14 total.
      var draw = new GameTile(new Tile(drawn), 999) { player = player };
      player.hand.pendingTile = draw;

      var hand14 = new List<GameTile>(player.hand.freeTiles) { draw };
      var action = new PlayTileAction(player, hand14, hand14[0]);
      return (game, ViewFor(game, 0), hand14, action);
    }

    [TestMethod]
    public void ChooseDiscard_PicksTheLowestShantenDiscard() {
      // Concealed 123m 456p 789s 1234s (13) + drawn isolated 7z. The 7z is
      // useless; discarding it keeps the best shape, while discarding a set tile
      // (e.g. 1m) raises shanten. The AI must not choose a higher-shanten discard.
      var (_, view, hand14, action) = DiscardTurn("123m456p789s1234s", "7z");

      int idx = RuleBasedStrategy.ChooseDiscardIndex(view, action);
      var chosen = action.options[idx].tile;

      var chosenEval = view.EvaluateDiscard(FindTile(hand14, chosen.tile), hand14);
      var breakRun = view.EvaluateDiscard(FindTile(hand14, new Tile("1m")), hand14);
      Assert.IsTrue(chosenEval.Shanten <= breakRun.Shanten,
          $"Chosen discard shanten {chosenEval.Shanten} should be <= " +
          $"breaking-a-set shanten {breakRun.Shanten}");
    }

    [TestMethod]
    public void EvaluateDiscard_LowerShantenForKeepingCompleteShapes() {
      // 123m456p789s1234s + 7z draw: discarding the isolated 7z keeps the hand
      // advancing, while discarding 1m (breaking the 123m run) is strictly worse.
      var (_, view, hand14, _) = DiscardTurn("123m456p789s1234s", "7z");

      var keepShape = view.EvaluateDiscard(FindTile(hand14, new Tile("7z")), hand14);
      var breakRun = view.EvaluateDiscard(FindTile(hand14, new Tile("1m")), hand14);
      Assert.IsTrue(keepShape.Shanten < breakRun.Shanten,
          $"Discarding the isolated 7z ({keepShape.Shanten}) should be closer to " +
          $"a win than breaking a run ({breakRun.Shanten})");
    }

    private static GameTile FindTile(List<GameTile> tiles, Tile kind) {
      return tiles.First(t => t.tile.IsSame(kind));
    }

    #endregion

    #region Efficiency & riichi

    [TestMethod]
    public void Decide_DeclaresRiichiWhenOfferedAWorthwhileWait() {
      // Concealed 123456789m 1122p (13, tenpai), draws 2p -> can discard a 2p and
      // riichi on a live wait.
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789m1122p")));
      var player = game.GetPlayer(0);
      var draw = new GameTile(new Tile("2p"), 999) { player = player };
      player.hand.pendingTile = draw;
      var view = ViewFor(game, 0);

      var hand14 = new List<GameTile>(player.hand.freeTiles) { draw };
      var riichi = new RiichiAction(player, hand14, hand14[0]);

      // Enough wall left and a live wait -> the AI should declare riichi.
      Assert.IsTrue(RuleBasedStrategy.ShouldRiichi(view, riichi));

      var inquiry = InquiryWith(0, riichi);
      var resp = RuleBasedStrategy.Decide(view, inquiry);
      Assert.AreEqual(inquiry.actions.IndexOf(riichi), resp.index);
      // The response encodes a valid discard option index for the riichi tile.
      int optionIdx = JsonSerializer.Deserialize<int>(resp.response);
      Assert.IsTrue(optionIdx >= 0 && optionIdx < riichi.options.Count);
    }

    [TestMethod]
    public void ChooseDiscard_KeepsTheHandTenpai() {
      // 123456789m 1122p (13, tenpai) + drawn 2p. Discarding a 2p keeps the hand
      // tenpai; the AI's efficiency scoring should pick a tenpai (shanten 0)
      // discard.
      var (_, view, _, action) = DiscardTurn("123456789m1122p", "2p");

      int idx = RuleBasedStrategy.ChooseDiscardIndex(view, action);
      var chosen = action.options[idx].tile;
      var candidate = action.candidates.Find(c => c.tile.tile.IsSame(chosen.tile));
      Assert.IsNotNull(candidate);
      Assert.IsTrue(candidate.tenpaiInfos.Count > 0,
          "The chosen discard should keep the hand tenpai");
    }

    #endregion

    #region Discard choice picks a legal option

    [TestMethod]
    public void ChooseDiscardIndex_ReturnsAValidOption() {
      var (_, view, _, action) = DiscardTurn("123456789m1234p", "5p");

      int idx = RuleBasedStrategy.ChooseDiscardIndex(view, action);
      Assert.IsTrue(idx >= 0 && idx < action.options.Count);

      // The encoded response round-trips to that option index.
      var resp = RuleBasedStrategy.Decide(view, InquiryWith(0, action));
      int encoded = JsonSerializer.Deserialize<int>(resp.response);
      Assert.IsTrue(encoded >= 0 && encoded < action.options.Count);
    }

    #endregion
  }
}
