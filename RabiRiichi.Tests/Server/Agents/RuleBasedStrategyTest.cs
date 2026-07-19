using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
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
    public void ShouldRiichi_DoesNotCountDoraTowardMinHan() {
      var game = BuildGame(b => b
          .WithConfig(c => c.SetMinHan(4))
          .WithPlayer(0, p => p.SetFreeTiles("123m456m789p345s1z")));
      var player = game.GetPlayer(0);
      var draw = new GameTile(new Tile("9m"), 998) { player = player };
      player.hand.pendingTile = draw;
      var hand14 = new List<GameTile>(player.hand.freeTiles) { draw };
      var riichi = new RiichiAction(player, hand14, hand14[0]);
      // Inflate total han with bonus value while leaving yaku han at zero. Even
      // with riichi's one yaku han, this is three han short of minHan=4.
      riichi.candidates = riichi.options.Select(option => new DiscardCandidate {
        tile = option.tile,
        tenpaiInfos = [new TenpaiInfo {
          winningTile = new Tile("7z"),
          han = 8,
          yaku = 0,
          fu = 30,
          points = 4000,
        }],
      }).ToList();

      Assert.IsFalse(RuleBasedStrategy.ShouldRiichi(ViewFor(game, 0), riichi));
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

    #region Kan / Nukidora / Call decisions

    /// <summary> Builds a self-turn tsumo GameTile (discardInfo == null). </summary>
    private static GameTile Tsumo(string tile, int traceId) =>
        new(new Tile(tile), traceId);

    /// <summary> Builds a tile taken from an opponent's discard (not tsumo). </summary>
    private static GameTile Claimed(Game game, string tile, int traceId, int fromSeat) {
      var gt = new GameTile(new Tile(tile), traceId);
      gt.discardInfo = new DiscardInfo(game.GetPlayer(fromSeat), DiscardReason.Draw, traceId);
      return gt;
    }

    [TestMethod]
    public void Decide_TakesKakanWhenPushing() {
      // Seat 0 already ponned 1z (east) and holds the fourth 1z as its draw.
      // Kakan upgrades the pon, reveals a new dora, and draws from the dead wall,
      // so a pushing player should take it rather than discard.
      // Free tiles must total 10 so that with the called pon (counts as 3) the
      // hand is a legal 13. The fourth 1z is the freshly drawn pending tile.
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("234567p2345s").AddCalled("111z", 0, 1)));
      var player = game.GetPlayer(0);
      var draw = Tsumo("1z", 900);
      draw.player = player;
      player.hand.pendingTile = draw;
      var view = ViewFor(game, 0);

      // The existing pon of 1z as a called Kou.
      var pon = (Kou)player.hand.called.First(m => m is Kou);
      var kakanOption = new List<GameTile>(pon) { draw };
      var kan = new KanAction(0, new List<List<GameTile>> { kakanOption });
      // A discard is also on the table: the kan must be chosen over tsumogiri,
      // which is exactly the case the old strategy mishandled.
      var hand14 = new List<GameTile>(player.hand.freeTiles) { draw };
      var play = new PlayTileAction(player, hand14, hand14[0]);

      var inquiry = InquiryWith(0, kan, play);
      var resp = RuleBasedStrategy.Decide(view, inquiry);
      Assert.AreEqual(inquiry.actions.IndexOf(kan), resp.index,
          "AI should declare the kakan when pushing, not just discard");
    }

    [TestMethod]
    public void Decide_TakesAnkanWhenItKeepsTenpai() {
      // Concealed 2345667p 234567s + four 1p (drawn the 4th). Ankan of 1p leaves a
      // clean waiting shape, so the AI should kan for the extra dora.
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("111p234567p2345s")));
      var player = game.GetPlayer(0);
      // Draw the fourth 1p, giving four concealed 1p.
      var draw = Tsumo("1p", 901);
      draw.player = player;
      player.hand.pendingTile = draw;
      var view = ViewFor(game, 0);

      var fourOfAKind = player.hand.freeTiles
          .Where(t => t.tile.IsSame(new Tile("1p")))
          .Concat(new[] { draw })
          .ToList();
      var kan = new KanAction(0, new List<List<GameTile>> { fourOfAKind });
      // Offer a discard too: the ankan must be preferred over tsumogiri.
      var hand14 = new List<GameTile>(player.hand.freeTiles) { draw };
      var play = new PlayTileAction(player, hand14, hand14[0]);

      var inquiry = InquiryWith(0, kan, play);
      var resp = RuleBasedStrategy.Decide(view, inquiry);
      Assert.AreEqual(inquiry.actions.IndexOf(kan), resp.index,
          "AI should ankan when it does not damage the hand");
    }

    [TestMethod]
    public void Decide_DoesNotKanWhileFolding() {
      // An opponent is in riichi and seat 0 is far from tenpai: taking an ankan
      // only reveals a dora for the aggressor and wastes a safe turn.
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("111p258m369s1234z"))
          .WithPlayer(1, p => p
              .SetFreeTiles("123456789p1234s")
              .SetRiichiTile("9s")));
      var player = game.GetPlayer(0);
      var draw = Tsumo("1p", 902);
      draw.player = player;
      player.hand.pendingTile = draw;
      var view = ViewFor(game, 0);

      var fourOfAKind = player.hand.freeTiles
          .Where(t => t.tile.IsSame(new Tile("1p")))
          .Concat(new[] { draw })
          .ToList();
      var kan = new KanAction(0, new List<List<GameTile>> { fourOfAKind });
      // Also offer a plain discard so declining the kan is a real alternative.
      var hand14 = new List<GameTile>(player.hand.freeTiles) { draw };
      var play = new PlayTileAction(player, hand14, hand14[0]);

      var inquiry = InquiryWith(0, kan, play);
      var resp = RuleBasedStrategy.Decide(view, inquiry);
      Assert.AreNotEqual(inquiry.actions.IndexOf(kan), resp.index,
          "AI should not kan while folding against a riichi");
    }

    [TestMethod]
    public void Decide_AlwaysTakesNukidora() {
      // Nukidora is a free dora that does not break concealment; always take it.
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("123456789p123s4z")));
      var player = game.GetPlayer(0);
      var view = ViewFor(game, 0);

      var north = player.hand.freeTiles.First(t => t.tile.IsSame(new Tile("4z")));
      var nuki = new NukiDoraAction(0, new List<List<GameTile>> { new() { north } });
      // Offer a discard too; the nuki should still win.
      var hand14 = new List<GameTile>(player.hand.freeTiles);
      var play = new PlayTileAction(player, hand14, hand14[0]);

      var inquiry = InquiryWith(0, nuki, play);
      var resp = RuleBasedStrategy.Decide(view, inquiry);
      Assert.AreEqual(inquiry.actions.IndexOf(nuki), resp.index,
          "AI should always pull North for the free dora");
    }

    [TestMethod]
    public void Decide_PonsValueHonorToAdvanceHand() {
      // Seat 0 holds two east (round + seat wind on East round for the dealer) and
      // an otherwise fast hand; ponning the third secures a yakuhai and advances.
      var game = BuildGame(b => b
          .WithConfig(c => c.SetMinHan(2))
          .WithPlayer(0, p => p.SetFreeTiles("11z23455p23456s7s")));
      var player = game.GetPlayer(0);
      var view = ViewFor(game, 0);

      var own = player.hand.freeTiles.Where(t => t.tile.IsSame(new Tile("1z"))).ToList();
      var claimed = Claimed(game, "1z", 903, 1);
      var group = new List<GameTile> { own[0], own[1], claimed };
      var pon = new PonAction(0, new List<List<GameTile>> { group });

      var inquiry = InquiryWith(0, pon);
      var resp = RuleBasedStrategy.Decide(view, inquiry);
      Assert.AreEqual(inquiry.actions.IndexOf(pon), resp.index,
          "Dealer's East is double yakuhai and meets minHan=2");
    }

    [TestMethod]
    public void Decide_DoesNotPonSingleYakuhaiBelowMinHan() {
      var game = BuildGame(b => b
          .WithConfig(c => c.SetMinHan(2))
          .WithPlayer(0, p => p.SetFreeTiles("55z23455p23456s7s")));
      var player = game.GetPlayer(0);
      var view = ViewFor(game, 0);

      var own = player.hand.freeTiles
          .Where(t => t.tile.IsSame(new Tile("5z"))).ToList();
      var claimed = Claimed(game, "5z", 908, 1);
      var pon = new PonAction(0, new List<List<GameTile>> {
        new() { own[0], own[1], claimed },
      });

      var inquiry = InquiryWith(0, pon);
      var resp = RuleBasedStrategy.Decide(view, inquiry);
      Assert.AreNotEqual(inquiry.actions.IndexOf(pon), resp.index,
          "One dragon yakuhai is only one yaku han and cannot meet minHan=2");
    }

    [TestMethod]
    public void Decide_DoesNotPonNonYakuhaiIntoAYakulessHand() {
      // Ponning a non-yakuhai triplet from a mixed-suit hand yields no realistic
      // yaku, so the open hand could never win: the AI must stay concealed.
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("22m567p13579s99p1z")));
      var player = game.GetPlayer(0);
      var view = ViewFor(game, 0);

      var own = player.hand.freeTiles.Where(t => t.tile.IsSame(new Tile("2m"))).ToList();
      var claimed = Claimed(game, "2m", 904, 1);
      var group = new List<GameTile> { own[0], own[1], claimed };
      var pon = new PonAction(0, new List<List<GameTile>> { group });

      var inquiry = InquiryWith(0, pon);
      var resp = RuleBasedStrategy.Decide(view, inquiry);
      Assert.AreNotEqual(inquiry.actions.IndexOf(pon), resp.index,
          "AI should not open a yakuless hand with a non-yakuhai pon");
    }

    [TestMethod]
    public void Decide_ChiiWhenItReachesTenpaiWithTanyao() {
      // A pure tanyao shape one tile from tenpai; chii-ing 4s5s(+3s) completes a
      // run into tenpai and the all-simples hand keeps a yaku, so a strong player
      // opens for the fast, valid hand.
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("234m456p22s45s678s")));
      var player = game.GetPlayer(0);
      var view = ViewFor(game, 0);

      var own = new List<GameTile> {
        player.hand.freeTiles.First(t => t.tile.IsSame(new Tile("4s"))),
        player.hand.freeTiles.First(t => t.tile.IsSame(new Tile("5s"))),
      };
      var claimed = Claimed(game, "3s", 905, 3);
      var group = new List<GameTile> { claimed, own[0], own[1] };
      var chii = new ChiiAction(0, new List<List<GameTile>> { group });

      var inquiry = InquiryWith(0, chii);
      var resp = RuleBasedStrategy.Decide(view, inquiry);
      Assert.AreEqual(inquiry.actions.IndexOf(chii), resp.index,
          "AI should chii into a tenpai tanyao hand");
    }

    [TestMethod]
    public void Decide_DoesNotChiiWhenItWouldStripTheOnlyYaku() {
      // A mixed-suit hand with terminals: chii-ing here opens a hand with no
      // tanyao/yakuhai/flush path, so it cannot win open. Stay concealed.
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("13m456p789p123s99s")));
      var player = game.GetPlayer(0);
      var view = ViewFor(game, 0);

      var own = new List<GameTile> {
        player.hand.freeTiles.First(t => t.tile.IsSame(new Tile("1m"))),
        player.hand.freeTiles.First(t => t.tile.IsSame(new Tile("3m"))),
      };
      var claimed = Claimed(game, "2m", 906, 3);
      var group = new List<GameTile> { own[0], claimed, own[1] };
      var chii = new ChiiAction(0, new List<List<GameTile>> { group });

      var inquiry = InquiryWith(0, chii);
      var resp = RuleBasedStrategy.Decide(view, inquiry);
      Assert.AreNotEqual(inquiry.actions.IndexOf(chii), resp.index,
          "AI should not open a yakuless terminal hand with a chii");
    }

    [TestMethod]
    public void Decide_DoesNotCallAfterRiichi() {
      // Once in riichi the concealed hand is locked; never open with a pon.
      var game = BuildGame(b => b
          .WithPlayer(0, p => p
              .SetFreeTiles("11z234567p23456s")
              .SetRiichiTile("9s")));
      var player = game.GetPlayer(0);
      var view = ViewFor(game, 0);

      var own = player.hand.freeTiles.Where(t => t.tile.IsSame(new Tile("1z"))).ToList();
      var claimed = Claimed(game, "1z", 907, 1);
      var group = new List<GameTile> { own[0], own[1], claimed };
      var pon = new PonAction(0, new List<List<GameTile>> { group });

      var inquiry = InquiryWith(0, pon);
      var resp = RuleBasedStrategy.Decide(view, inquiry);
      Assert.AreNotEqual(inquiry.actions.IndexOf(pon), resp.index,
          "AI in riichi must not open its hand");
    }

    #endregion
  }
}
