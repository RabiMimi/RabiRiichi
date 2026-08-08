using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Arena.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RabiRiichi.Tests.Server.Arena {
  [TestClass]
  public class RatingServiceTest {
    private string workspaceDir;

    [TestInitialize]
    public void Setup() {
      workspaceDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_rating_{Guid.NewGuid():N}");
      Directory.CreateDirectory(workspaceDir);
    }

    [TestCleanup]
    public void Cleanup() {
      if (Directory.Exists(workspaceDir)) {
        try { Directory.Delete(workspaceDir, recursive: true); } catch { }
      }
    }

    private static RatingService Service(double k = 24, double initial = 1500) =>
        new(k, initial);

    private static RatingParticipant P(
        string id, double elo, int placement, double? frozen = null,
        int penalties = 0) => new() {
      ModelId = id,
      Elo = elo,
      Placement = placement,
      FrozenElo = frozen,
      PenaltyCount = penalties,
    };

    // ----- Pure Elo math ---------------------------------------------------

    [TestMethod]
    public void ExpectedScore_EqualRatingsIsHalf() {
      Assert.AreEqual(0.5, RatingService.ExpectedScore(1500, 1500), 1e-9);
    }

    [TestMethod]
    public void ExpectedScore_HigherRatingFavoured() {
      double e = RatingService.ExpectedScore(1600, 1400);
      Assert.IsTrue(e > 0.5 && e < 1.0);
      // Symmetry: the pair sums to 1.
      Assert.AreEqual(1.0,
          e + RatingService.ExpectedScore(1400, 1600), 1e-9);
    }

    [TestMethod]
    public void PairwiseScore_WinLossTie() {
      Assert.AreEqual(1.0, RatingService.PairwiseScore(1, 2));
      Assert.AreEqual(0.0, RatingService.PairwiseScore(3, 1));
      Assert.AreEqual(0.5, RatingService.PairwiseScore(2, 2));
    }

    [TestMethod]
    public void ComputeDeltas_ClearFinishMovesRatingsInRightDirection() {
      // Equal ratings, clean 1>2>3>4 finish.
      var parts = new List<RatingParticipant> {
        P("a", 1500, 1),
        P("b", 1500, 2),
        P("c", 1500, 3),
        P("d", 1500, 4),
      };
      var d = Service().ComputeEloDeltas(parts);

      // 1st gains the most, 4th loses the most; monotonic ordering.
      Assert.IsTrue(d["a"] > d["b"]);
      Assert.IsTrue(d["b"] > d["c"]);
      Assert.IsTrue(d["c"] > d["d"]);

      // 1st strictly gains, 4th strictly loses.
      Assert.IsTrue(d["a"] > 0);
      Assert.IsTrue(d["d"] < 0);

      // Zero-sum among equal-rated players (no frozen anchors).
      Assert.AreEqual(0.0, d.Values.Sum(), 1e-9);
    }

    [TestMethod]
    public void ComputeDeltas_EqualRatingsMagnitudesMatchExpected() {
      // With equal ratings, expected pairwise score is 0.5 for every pair, so
      // delta = K * sum(actual - 0.5). For 1st (beats all 3): K*(3*(1-0.5)) =
      // 24 * 1.5 = 36. For 4th: 24 * (3*(0-0.5)) = -36. For 2nd: wins vs 3,4,
      // loses vs 1 -> 24*((1-0.5)+(1-0.5)+(0-0.5)) = 24*0.5 = 12.
      var parts = new List<RatingParticipant> {
        P("a", 1500, 1),
        P("b", 1500, 2),
        P("c", 1500, 3),
        P("d", 1500, 4),
      };
      var d = Service(k: 24).ComputeEloDeltas(parts);
      Assert.AreEqual(36.0, d["a"], 1e-9);
      Assert.AreEqual(12.0, d["b"], 1e-9);
      Assert.AreEqual(-12.0, d["c"], 1e-9);
      Assert.AreEqual(-36.0, d["d"], 1e-9);
    }

    [TestMethod]
    public void ComputeDeltas_TieSplitsPairwisePointsEvenly() {
      // a and b tie for 1st (placement 1), c is 3rd, d is 4th.
      var parts = new List<RatingParticipant> {
        P("a", 1500, 1),
        P("b", 1500, 1),
        P("c", 1500, 3),
        P("d", 1500, 4),
      };
      var d = Service(k: 24).ComputeEloDeltas(parts);
      // a and b are symmetric -> equal deltas.
      Assert.AreEqual(d["a"], d["b"], 1e-9);
      // Both tied-for-first beat c and d and split with each other:
      // 24*((0.5-0.5)+(1-0.5)+(1-0.5)) = 24*1.0 = 24.
      Assert.AreEqual(24.0, d["a"], 1e-9);
    }

    [TestMethod]
    public void ComputeDeltas_FrozenBaselineDoesNotMoveButInfluencesOthers() {
      // A strong frozen baseline at 1600 places 1st; three LLMs at 1500.
      var parts = new List<RatingParticipant> {
        P("base", 1600, 1, frozen: 1600),
        P("x", 1500, 2),
        P("y", 1500, 3),
        P("z", 1500, 4),
      };
      var d = Service().ComputeEloDeltas(parts);

      // Frozen anchor never moves.
      Assert.AreEqual(0.0, d["base"], 1e-9);

      // The others are still rated relative to the anchor: x (2nd) beats y,z and
      // loses to the stronger anchor, but the anchor was expected to win anyway.
      Assert.IsTrue(d["x"] > d["y"]);
      Assert.IsTrue(d["y"] > d["z"]);
      Assert.IsTrue(d["z"] < 0);
    }

    [TestMethod]
    public void ComputeDeltas_LosingToWeakerAnchorPenalizesMore() {
      // A weak anchor at 1400 nonetheless places 1st: the 1500 LLM that came 4th
      // loses more than if the winner had been an equal-rated player, because it
      // was expected to beat the weaker anchor.
      var withWeakAnchorWinner = new List<RatingParticipant> {
        P("weak", 1400, 1, frozen: 1400),
        P("z", 1500, 4),
        P("p", 1500, 2),
        P("q", 1500, 3),
      };
      var d = Service().ComputeEloDeltas(withWeakAnchorWinner);
      Assert.AreEqual(0.0, d["weak"], 1e-9);
      Assert.IsTrue(d["z"] < 0);
    }

    // ----- Store application ------------------------------------------------

    [TestMethod]
    public void ApplyMatch_NewModelsStartAtInitialElo() {
      var store = new RatingStore(workspaceDir);
      var svc = Service(k: 24, initial: 1500);
      var parts = new List<RatingParticipant> {
        P("a", 1500, 1),
        P("b", 1500, 2),
        P("c", 1500, 3),
        P("d", 1500, 4),
      };
      var changes = svc.ApplyMatch(store, parts);

      // Before-values are the initial Elo for brand-new models.
      Assert.AreEqual(1500.0, changes["a"].EloBefore, 1e-9);
      Assert.AreEqual(1500.0 + 36.0, changes["a"].EloAfter, 1e-9);
      Assert.AreEqual(1500.0 - 36.0, changes["d"].EloAfter, 1e-9);

      // Persisted.
      Assert.AreEqual(changes["a"].EloAfter, store.Get("a").Elo, 1e-9);
    }

    [TestMethod]
    public void ApplyMatch_CountersIncrement() {
      var store = new RatingStore(workspaceDir);
      var svc = Service();
      var parts = new List<RatingParticipant> {
        P("a", 1500, 1, penalties: 2),
        P("b", 1500, 2),
        P("c", 1500, 3),
        P("d", 1500, 4),
      };
      svc.ApplyMatch(store, parts);

      var a = store.Get("a");
      Assert.AreEqual(1, a.Games);
      Assert.AreEqual(1, a.Wins);
      Assert.AreEqual(1, a.Place1);
      Assert.AreEqual(0, a.Place2);
      Assert.AreEqual(2, a.Penalties);

      var d = store.Get("d");
      Assert.AreEqual(1, d.Games);
      Assert.AreEqual(0, d.Wins);
      Assert.AreEqual(1, d.Place4);

      // A second match accumulates counters.
      svc.ApplyMatch(store, new List<RatingParticipant> {
        P("a", store.Get("a").Elo, 2),
        P("b", store.Get("b").Elo, 1),
        P("c", store.Get("c").Elo, 3),
        P("d", store.Get("d").Elo, 4),
      });
      var a2 = store.Get("a");
      Assert.AreEqual(2, a2.Games);
      Assert.AreEqual(1, a2.Wins);
      Assert.AreEqual(1, a2.Place1);
      Assert.AreEqual(1, a2.Place2);
    }

    [TestMethod]
    public void ApplyMatch_FrozenBaselineEloUnchangedAfterPersist() {
      var store = new RatingStore(workspaceDir);
      var svc = Service();
      var parts = new List<RatingParticipant> {
        P("base", 1600, 4, frozen: 1600), // even losing, anchor is pinned
        P("x", 1500, 1),
        P("y", 1500, 2),
        P("z", 1500, 3),
      };
      var changes = svc.ApplyMatch(store, parts);

      Assert.AreEqual(1600.0, changes["base"].EloBefore, 1e-9);
      Assert.AreEqual(1600.0, changes["base"].EloAfter, 1e-9);
      Assert.AreEqual(1600.0, store.Get("base").Elo, 1e-9);
      // But its counters still update.
      Assert.AreEqual(1, store.Get("base").Games);
      Assert.AreEqual(1, store.Get("base").Place4);

      // The LLMs still moved.
      Assert.AreNotEqual(1500.0, store.Get("x").Elo);
    }

    [TestMethod]
    public void ApplyMatch_DuplicatedBaselineSeatsFoldIntoOneRecord() {
      var store = new RatingStore(workspaceDir);
      var svc = Service();
      // Two seats share the same baseline id (table padding, §10).
      var parts = new List<RatingParticipant> {
        P("dup", 1500, 1, frozen: 1500),
        P("dup", 1500, 4, frozen: 1500),
        P("x", 1500, 2),
        P("y", 1500, 3),
      };
      svc.ApplyMatch(store, parts);
      var dup = store.Get("dup");
      // Both seats counted under one record.
      Assert.AreEqual(2, dup.Games);
      Assert.AreEqual(1, dup.Place1);
      Assert.AreEqual(1, dup.Place4);
      Assert.AreEqual(1500.0, dup.Elo, 1e-9);
    }
  }
}
