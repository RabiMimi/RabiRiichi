using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Arena.Storage;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Tests.Server.Arena {
  /// <summary>
  /// Pure Swiss pairing tests (ARENA_DESIGN.md §10): table count, similar-standing
  /// grouping, rematch avoidance across rounds, and baseline filling when the
  /// roster is not a multiple of 4. No I/O, no game play.
  /// </summary>
  [TestClass]
  public class SwissSchedulerTest {
    private static ArenaConfig.ModelConfig Llm(string id) => new() {
      Id = id,
      DisplayName = id.ToUpperInvariant(),
      Provider = "openai",
      Model = "m",
      Enabled = true,
    };

    private static ArenaConfig.ModelConfig Baseline(string id, double frozen = 1500) => new() {
      Id = id,
      DisplayName = id.ToUpperInvariant(),
      Provider = "baseline",
      Variant = "default",
      FrozenElo = frozen,
      Enabled = true,
    };

    private static SwissStanding S(ArenaConfig.ModelConfig m, double elo) =>
        new() { Model = m, Elo = elo };

    private static IReadOnlyList<ArenaConfig.ModelConfig> Fillers(params string[] ids) =>
        ids.Select(id => Baseline(id)).ToList();

    // ----- Table count + validity -----------------------------------------

    [TestMethod]
    public void Pair_EightPlayers_TwoTablesOfFour() {
      var swiss = new SwissScheduler(4);
      var standings = Enumerable.Range(0, 8)
          .Select(i => S(Llm($"p{i}"), 1500 + i))
          .ToList();

      var tables = swiss.Pair(standings, null, Fillers("base"));

      Assert.AreEqual(2, tables.Count);
      foreach (var t in tables) {
        Assert.AreEqual(4, t.Assignments.Count);
        Assert.IsFalse(t.Padded);
        // Seats are 0..3 exactly once.
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 },
            t.Assignments.Select(a => a.Seat).OrderBy(x => x).ToList());
      }

      // Every player is placed exactly once across the two tables.
      var allIds = tables.SelectMany(t => t.Assignments.Select(a => a.Model.Id))
          .OrderBy(x => x).ToList();
      CollectionAssert.AreEqual(
          Enumerable.Range(0, 8).Select(i => $"p{i}").OrderBy(x => x).ToList(),
          allIds);
    }

    [TestMethod]
    public void Pair_FourPlayers_SingleTable() {
      var swiss = new SwissScheduler(4);
      var standings = new[] {
        S(Llm("a"), 1500), S(Llm("b"), 1500), S(Llm("c"), 1500), S(Llm("d"), 1500),
      };
      var tables = swiss.Pair(standings, null, Fillers("base"));
      Assert.AreEqual(1, tables.Count);
      Assert.IsFalse(tables[0].Padded);
    }

    // ----- Grouping by standing -------------------------------------------

    [TestMethod]
    public void Pair_GroupsByStanding_TopFourTogether() {
      var swiss = new SwissScheduler(4);
      // Distinct Elos so the grouping is unambiguous.
      var standings = new[] {
        S(Llm("h1"), 1800), S(Llm("h2"), 1790), S(Llm("h3"), 1780), S(Llm("h4"), 1770),
        S(Llm("l1"), 1400), S(Llm("l2"), 1390), S(Llm("l3"), 1380), S(Llm("l4"), 1370),
      };

      var tables = swiss.Pair(standings, null, Fillers("base"));

      Assert.AreEqual(2, tables.Count);
      var topTable = tables[0].Assignments.Select(a => a.Model.Id).OrderBy(x => x).ToList();
      CollectionAssert.AreEqual(new[] { "h1", "h2", "h3", "h4" }, topTable);
      var botTable = tables[1].Assignments.Select(a => a.Model.Id).OrderBy(x => x).ToList();
      CollectionAssert.AreEqual(new[] { "l1", "l2", "l3", "l4" }, botTable);
    }

    // ----- Filling with baselines -----------------------------------------

    [TestMethod]
    public void Pair_NotMultipleOfFour_PadsShortTableWithBaselines() {
      var swiss = new SwissScheduler(4);
      // 6 players -> one full table + one short table padded with 2 fillers.
      var standings = Enumerable.Range(0, 6)
          .Select(i => S(Llm($"p{i}"), 1500 + i))
          .ToList();

      var tables = swiss.Pair(standings, null, Fillers("base-fill"));

      Assert.AreEqual(2, tables.Count);
      Assert.IsTrue(tables.All(t => t.Assignments.Count == 4));

      var padded = tables.Single(t => t.Padded);
      int fillerSeats = padded.Assignments.Count(a => a.Model.Id == "base-fill");
      Assert.AreEqual(2, fillerSeats, "short table padded with 2 baseline fillers");

      // No byes: every seat has a real model.
      Assert.IsTrue(tables.All(t => t.Assignments.All(a => a.Model != null)));
    }

    [TestMethod]
    public void Pair_NotMultipleOfFour_NoFiller_Throws() {
      var swiss = new SwissScheduler(4);
      var standings = Enumerable.Range(0, 5)
          .Select(i => S(Llm($"p{i}"), 1500 + i))
          .ToList();

      Assert.ThrowsException<InvalidOperationException>(
          () => swiss.Pair(standings, null, new List<ArenaConfig.ModelConfig>()));
    }

    [TestMethod]
    public void Pair_Filler_PrefersFrozenBaseline() {
      var swiss = new SwissScheduler(4);
      var standings = Enumerable.Range(0, 5)
          .Select(i => S(Llm($"p{i}"), 1500 + i))
          .ToList();
      var nonFrozen = Llm("not-a-baseline"); // no FrozenElo
      var frozen = Baseline("frozen-base", 1500);

      var tables = swiss.Pair(standings, null, new[] { nonFrozen, frozen });

      var padded = tables.Single(t => t.Padded);
      Assert.IsTrue(padded.Assignments.Any(a => a.Model.Id == "frozen-base"),
          "frozen baseline preferred as filler");
    }

    // ----- Rematch avoidance ----------------------------------------------

    [TestMethod]
    public void Pair_AvoidsRematchesAcrossRounds_WhenPossible() {
      var swiss = new SwissScheduler(4);
      // 8 equal-Elo players; round 1 pairs them somehow.
      var models = Enumerable.Range(0, 8).Select(i => Llm($"p{i}")).ToList();
      var round1 = swiss.Pair(
          models.Select(m => S(m, 1500)).ToList(), null, Fillers("base"));

      var prior = round1.Select(t => (IReadOnlyCollection<string>)t.ModelIds.ToList())
          .ToList();

      // Round 2 with the same standings should avoid re-pairing the same tables.
      var round2 = swiss.Pair(
          models.Select(m => S(m, 1500)).ToList(), prior, Fillers("base"));

      // Count how many unordered pairs repeat between round 1 and round 2.
      var r1Pairs = PairsOf(round1);
      var r2Pairs = PairsOf(round2);
      int repeats = r2Pairs.Count(p => r1Pairs.Contains(p));

      // With 8 players and identical standings a perfect no-rematch round exists;
      // the greedy scheduler should achieve a strict reduction (best-effort §10).
      Assert.IsTrue(repeats < r1Pairs.Count,
          $"round 2 should reduce rematches (repeats={repeats}, r1Pairs={r1Pairs.Count})");
    }

    [TestMethod]
    public void Pair_Deterministic_SameInputsSameOutput() {
      var swiss = new SwissScheduler(4);
      var standings = Enumerable.Range(0, 8)
          .Select(i => S(Llm($"p{i}"), 1500 + i))
          .ToList();

      var a = swiss.Pair(standings, null, Fillers("base"));
      var b = swiss.Pair(standings, null, Fillers("base"));

      var aIds = a.Select(t => string.Join(",", t.ModelIds)).ToList();
      var bIds = b.Select(t => string.Join(",", t.ModelIds)).ToList();
      CollectionAssert.AreEqual(aIds, bIds);
    }

    private static HashSet<string> PairsOf(IReadOnlyList<SwissTable> tables) {
      var set = new HashSet<string>();
      foreach (var t in tables) {
        var ids = t.ModelIds.ToList();
        for (int i = 0; i < ids.Count; i++) {
          for (int j = i + 1; j < ids.Count; j++) {
            set.Add($"{ids[i]}|{ids[j]}");
          }
        }
      }
      return set;
    }
  }
}
