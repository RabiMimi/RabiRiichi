using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Server.Agents;
using RabiRiichi.Tests.Scenario;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Tests.Server.Agents {
  /// <summary>
  /// Tests for the yaku-route planner, which is what lets the agent respect the
  /// table's minimum-han rule: it answers "can this shape still produce enough
  /// yaku han to be a legal win", separately from "is this shape efficient".
  /// </summary>
  [TestClass]
  public class HandPlanTest {
    private static Game BuildGame(System.Action<ScenarioBuilder> configure) {
      var builder = new ScenarioBuilder();
      configure(builder);
      Game game = null;
      builder.Build(0).WithGame(g => game = g);
      return game;
    }

    private static PublicGameView ViewOf(Game game) => new(game, 0);

    private static HandPlan PlanFor(PublicGameView view) =>
        HandPlan.Analyze(view, view.SelfHand.freeTiles, view.SelfHand.called);

    private static int DistanceOf(HandPlan plan, YakuTag tag) =>
        plan.Routes.Where(r => r.Tag == tag)
            .Select(r => r.Distance)
            .DefaultIfEmpty(int.MaxValue)
            .Min();

    #region Yakuhai

    [TestMethod]
    public void DealerEastTripletIsWorthTwoYakuHan() {
      // Seat 0 is the dealer in an East round, so its East triplet is both the
      // round wind and the seat wind: two separate yaku, not one.
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("111z234m567m234p9p")));
      var plan = PlanFor(ViewOf(game));

      var yakuhai = plan.Routes.Where(r => r.Tag == YakuTag.Yakuhai && r.Distance == 0)
          .ToList();
      Assert.AreEqual(2, yakuhai.Count,
          "Round wind and seat wind are separate yaku for a dealer's East");
      Assert.IsTrue(plan.BestYakuHan(open: true, budget: 0) >= 2);
    }

    [TestMethod]
    public void ADragonPairIsOnePonAwayButADragonlessHandIsNot() {
      var withPair = PlanFor(ViewOf(BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("55z234m567m234p99p")))));
      var without = PlanFor(ViewOf(BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("34m234m567m234p99p")))));

      Assert.IsTrue(DistanceOf(withPair, YakuTag.Yakuhai)
          < DistanceOf(without, YakuTag.Yakuhai));
    }

    #endregion

    #region Compatibility

    [TestMethod]
    public void TanyaoAndYakuhaiDoNotStack() {
      Assert.IsFalse(HandPlan.Compatible(YakuTag.Tanyao, YakuTag.Yakuhai));
      Assert.IsFalse(HandPlan.Compatible(YakuTag.Yakuhai, YakuTag.Tanyao));
    }

    [TestMethod]
    public void TwoDifferentYakuhaiDoStack() {
      Assert.IsTrue(HandPlan.Compatible(YakuTag.Yakuhai, YakuTag.Yakuhai));
    }

    [TestMethod]
    public void FlushesAreAlternativesNotAdditions() {
      Assert.IsFalse(HandPlan.Compatible(YakuTag.Honitsu, YakuTag.Chinitsu));
      // Three-colour runs need all three suits, so no flush can carry them.
      Assert.IsFalse(HandPlan.Compatible(YakuTag.Honitsu, YakuTag.SanshokuDoujun));
      // A straight lives in one suit, so it is fine alongside a flush.
      Assert.IsTrue(HandPlan.Compatible(YakuTag.Chinitsu, YakuTag.Ittsu));
    }

    [TestMethod]
    public void SevenPairsExcludesEveryRunAndTripletYaku() {
      Assert.IsFalse(HandPlan.Compatible(YakuTag.Chiitoitsu, YakuTag.Toitoi));
      Assert.IsFalse(HandPlan.Compatible(YakuTag.Chiitoitsu, YakuTag.Pinfu));
      Assert.IsFalse(HandPlan.Compatible(YakuTag.Chiitoitsu, YakuTag.Ittsu));
      Assert.IsTrue(HandPlan.Compatible(YakuTag.Chiitoitsu, YakuTag.Honitsu));
    }

    #endregion

    #region What a kan destroys

    [TestMethod]
    public void AConcealedKanRemovesPinfuAndSevenPairsFromThePlan() {
      // Same thirteen tiles, once as a plain concealed hand and once with four of
      // them locked away as an ankan. A kan keeps the hand menzen, but it is a
      // triplet set aside: pinfu, seven pairs and thirteen orphans all die with
      // it. This is exactly the trap the kan policy has to see.
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("1111p234567p234s")));
      var view = ViewOf(game);
      var hand = view.SelfHand.freeTiles;

      var concealed = HandPlan.Analyze(view, hand, []);
      Assert.IsTrue(concealed.Routes.Any(r => r.Tag == YakuTag.Pinfu));
      Assert.IsTrue(concealed.Routes.Any(r => r.Tag == YakuTag.Chiitoitsu));

      var kanTiles = hand.Where(t => t.tile.IsSame(new Tile("1p"))).ToList();
      var rest = hand.Where(t => !t.tile.IsSame(new Tile("1p"))).ToList();
      var afterKan = HandPlan.Analyze(
          view, rest, [new Kan(kanTiles, TileSource.Ankan)]);

      Assert.IsTrue(afterKan.Menzen, "An ankan keeps the hand concealed");
      Assert.IsFalse(afterKan.Routes.Any(r => r.Tag == YakuTag.Pinfu),
          "A kan is a triplet, so pinfu is impossible");
      Assert.IsFalse(afterKan.Routes.Any(r => r.Tag == YakuTag.Chiitoitsu),
          "Four identical tiles cannot be part of seven pairs");
      // Riichi survives, so the hand is still legal on a one-han table.
      Assert.IsTrue(afterKan.Routes.Any(r => r.Tag == YakuTag.Riichi));
    }

    [TestMethod]
    public void AnOpenMeldRemovesEveryConcealedOnlyRoute() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("234m567m234p567p9p")));
      var view = ViewOf(game);
      var hand = view.SelfHand.freeTiles;

      var meldTiles = hand.Take(3).ToList();
      meldTiles[0].discardInfo =
          new DiscardInfo(game.GetPlayer(1), DiscardReason.Draw, 1);
      var open = HandPlan.Analyze(
          view, hand.Skip(3).ToList(), [new Shun(meldTiles)]);

      Assert.IsFalse(open.Menzen);
      foreach (var concealedOnly in new[] {
        YakuTag.Riichi, YakuTag.MenzenTsumo, YakuTag.Pinfu,
        YakuTag.Chiitoitsu, YakuTag.Kokushi,
      }) {
        Assert.IsFalse(open.Routes.Any(r => r.Tag == concealedOnly),
            $"{concealedOnly} requires a concealed hand");
      }
      // All that is left is open tanyao, one tile away (the lone 9p has to go).
      Assert.AreEqual(0, open.BestYakuHan(open: true, budget: 0));
      Assert.AreEqual(1, open.BestYakuHan(open: true, budget: 1));
    }

    #endregion

    #region Minimum-han feasibility

    [TestMethod]
    public void AnOpenTerminalHandCannotReachATwoHanTable() {
      // Ponned 5z (one yakuhai) with a scattered three-suit hand: no tanyao (the
      // meld is an honor), no flush, no straight within reach.
      var game = BuildGame(b => b
          .WithConfig(c => c.SetMinHan(2))
          .WithPlayer(0, p => p.SetFreeTiles("234m567p13s99s").AddCalled("555z", 0, 1)));
      var view = ViewOf(game);
      var plan = PlanFor(view);

      Assert.AreEqual(1, plan.BestYakuHan(open: true, budget: 2),
          "Only the single white-dragon yakuhai is within reach");
      Assert.IsFalse(plan.CanReachMinHan(view, open: true, budget: 2));
    }

    [TestMethod]
    public void TheSameHandIsLegalOnAOneHanTable() {
      var game = BuildGame(b => b
          .WithConfig(c => c.SetMinHan(1))
          .WithPlayer(0, p => p.SetFreeTiles("234m567p13s99s").AddCalled("555z", 0, 1)));
      var view = ViewOf(game);
      Assert.IsTrue(PlanFor(view).CanReachMinHan(view, open: true, budget: 2));
    }

    [TestMethod]
    public void ACalledTerminalMeldKillsTanyao() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("234m567p234s5s").AddCalled("111m", 0, 1)));
      var plan = PlanFor(ViewOf(game));
      Assert.IsFalse(plan.Routes.Any(r => r.Tag == YakuTag.Tanyao),
          "A meld containing a terminal can never be part of an all-simples hand");
    }

    [TestMethod]
    public void ACleanSimplesHandHasTanyaoAtDistanceZero() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("234m567m234p567p8p")));
      Assert.AreEqual(0, DistanceOf(PlanFor(ViewOf(game)), YakuTag.Tanyao));
    }

    [TestMethod]
    public void DistanceToMinHanGrowsWithTheRequirement() {
      var game = BuildGame(b => b
          .WithConfig(c => c.SetMinHan(3))
          .WithPlayer(0, p => p.SetFreeTiles("234m567m234p567p8p")));
      var view = ViewOf(game);
      var plan = PlanFor(view);

      // Closed, this all-simples shape already carries riichi + tanyao + pinfu.
      Assert.AreEqual(3, plan.BestYakuHan(open: false, budget: 0));
      Assert.AreEqual(0, plan.DistanceToMinHan(view, open: false));

      // Opening it throws away riichi and pinfu, leaving only tanyao - so the
      // three-han requirement suddenly becomes a long way off.
      Assert.AreEqual(1, plan.BestYakuHan(open: true, budget: 0));
      Assert.IsTrue(plan.DistanceToMinHan(view, open: true) > 0,
          "Opening the hand throws away riichi, so min-han gets further away");
    }

    #endregion

    #region Budget

    [TestMethod]
    public void BudgetIsZeroAtTenpaiAndGrowsWithShanten() {
      var game = BuildGame(b => b
          .WithPlayer(0, p => p.SetFreeTiles("234m567m234p567p8p")));
      var view = ViewOf(game);

      Assert.AreEqual(0, HandPlan.BudgetFor(view, 0));
      Assert.IsTrue(HandPlan.BudgetFor(view, 3) > HandPlan.BudgetFor(view, 1));
    }

    #endregion
  }
}
