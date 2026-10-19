using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Core.Setup;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns;
using RabiRiichi.Patterns.Compiled;
using RabiRiichi.Tests.Helper;
using System;
using System.Diagnostics;
using System.Linq;

namespace RabiRiichi.Tests.Patterns {
  internal sealed class AllKoyakuSetup : RiichiSetup {
    protected override void InitPatterns() {
      base.InitPatterns();
      foreach (var pattern in KoyakuPatterns.ALL) AddStdPattern(pattern);
    }
  }

  internal static class WildcardFixture {
    internal static Game Game(bool koyaku = false) {
      var game = new Game(new GameConfig {
        actionCenter = new JsonStringActionCenter(null),
        setup = koyaku ? new AllKoyakuSetup() : new RiichiSetup(),
      });
      foreach (var p in game.players) { p.Reset(); p.hand.jun = 2; }
      game.wall.Reset();
      return game;
    }
    internal static GameTile Incoming(string tile, bool tsumo = true) => new(new Tile(tile), -1) {
      source = tsumo ? TileSource.Wall : TileSource.Discard,
      discardInfo = tsumo ? null : new DiscardInfo(null, DiscardReason.Draw, 0),
    };
    internal static Hand Hand(Game game, string tiles) {
      var hand = game.players[0].hand;
      hand.freeTiles = new Tiles(tiles).ToGameTileList();
      return hand;
    }
    internal static void Same(ScoreStorage expected, ScoreStorage actual, string label) {
      if (expected == null) { Assert.IsNull(actual, label); return; }
      Assert.IsNotNull(actual, label);
      Assert.AreEqual(expected.result.han, actual.result.han, label + " han");
      Assert.AreEqual(expected.result.yaku, actual.result.yaku, label + " yaku");
      Assert.AreEqual(expected.result.fu, actual.result.fu, label + " fu");
      Assert.AreEqual(expected.result.yakuman, actual.result.yakuman, label + " yakuman");
      Assert.AreEqual(expected.result.bonusYakuman, actual.result.bonusYakuman, label + " bonus yakuman");
      CollectionAssert.AreEqual(expected.Select(s => $"{s.Src}:{s.Type}:{s.Val}").Order().ToArray(),
          actual.Select(s => $"{s.Src}:{s.Type}:{s.Val}").Order().ToArray(), label + " entries");
    }
  }

  [TestClass]
  public class WildcardScoringTest {
    [TestMethod]
    public void AllWildcardsFindsExactHighScoreWithoutEnumeratingAssignments() {
      var game = WildcardFixture.Game();
      var hand = WildcardFixture.Hand(game, new string('*', 13));
      var incoming = WildcardFixture.Incoming("*");
      var original = hand.freeTiles.ToArray();
      var clock = Stopwatch.StartNew();
      var score = game.Get<PatternResolver>().ResolveBestValidScore(hand, incoming, PatternMask.All, 1);
      Assert.IsNotNull(score);
      // Custom multiplicities are supported: a wind may be both triplet and pair.
      Assert.AreEqual(6, score.result.yakuman);
      Assert.AreEqual(48000L, score.result.BaseScore);
      Assert.IsTrue(score.resolution.VisitedTargets < 100000, $"visited {score.resolution.VisitedTargets} targets in {clock.Elapsed}");
      CollectionAssert.AreEqual(original, hand.freeTiles.ToArray());
      Assert.IsTrue(hand.freeTiles.All(t => t.tile.IsWildcard));
      Assert.IsTrue(incoming.tile.IsWildcard);
      Assert.AreEqual(14, score.resolution.Groups.Sum(g => g.Tiles.Count));
      Assert.AreEqual(14, score.resolution.Groups.SelectMany(g => g.Tiles).Select(t => t.Physical).Distinct().Count());
      Assert.IsTrue(score.resolution.Groups.SelectMany(g => g.Tiles).All(t => t.Effective.IsValid && !t.Effective.IsWildcard));
      Console.WriteLine($"all-wild score: {clock.Elapsed.TotalMilliseconds:F2} ms; targets={score.resolution.VisitedTargets}; roles={score.resolution.EvaluatedInterpretations}");
    }

    [TestMethod]
    public void AllWaitsBatchMatchesIndividualSearch() {
      var game = WildcardFixture.Game();
      var hand = WildcardFixture.Hand(game, "123m456p789s11z**");
      var resolver = game.Get<PatternResolver>();
      var waits = hand.Tenpai;
      var batch = resolver.ResolveScorePreviews(hand, waits, PatternMask.All, PatternMask.Luck);
      foreach (var tile in waits) {
        WildcardFixture.Same(resolver.ResolveMaxScore(hand, WildcardFixture.Incoming(tile.ToString(), false), PatternMask.All, PatternMask.Luck), batch[tile].Ron, tile + " ron");
        WildcardFixture.Same(resolver.ResolveMaxScore(hand, WildcardFixture.Incoming(tile.ToString()), PatternMask.All, PatternMask.Luck), batch[tile].Tsumo, tile + " tsumo");
      }
    }

    [TestMethod]
    public void WildcardHasNoRepresentedDoraAndRetainsPhysicalIdentity() {
      var game = WildcardFixture.Game();
      var hand = WildcardFixture.Hand(game, "123m456p789s111z*");
      game.wall.doras.Clear();
      game.wall.doras.Add(new(new Tile("1z"), 50)); // South is dora.
      game.wall.revealedDoraCount = 1;
      var incoming = WildcardFixture.Incoming("2z");
      var score = game.Get<PatternResolver>().ResolveMaxScore(hand, incoming, PatternMask.All);
      Assert.IsNotNull(score);
      Assert.AreEqual(1, score.Single(s => s.Source is Dora).Val);
      Assert.AreEqual(new Tile("2z"), score.resolution.Groups.SelectMany(g => g.Tiles).Single(t => t.Physical.tile.IsWildcard).Effective);
      Assert.IsTrue(hand.freeTiles[^1].tile.IsWildcard);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SmallWildcardSearchMatchesExplicitSubstitution(bool wildcardIncoming) {
      var game = WildcardFixture.Game(true);
      var resolver = game.Get<PatternResolver>();
      var hand = WildcardFixture.Hand(game, wildcardIncoming ? "111m222p333s44z56s" : "111m222p333s44z5s*");
      var incoming = WildcardFixture.Incoming(wildcardIncoming ? "*" : "6s", false);
      ScoreStorage expected = null;
      foreach (var replacement in Tiles.AllDistinct) {
        var free = hand.freeTiles.Select(t => new GameTile(t.tile.IsWildcard ? replacement : t.tile, t.traceId)).ToList();
        var tile = WildcardFixture.Incoming((wildcardIncoming ? replacement : incoming.tile).ToString(), false);
        var candidate = resolver.ResolveMaxScoreLegacy(hand.WithFreeTiles(free), tile, PatternMask.All);
        if (candidate != null && (expected == null || candidate.CompareTo(expected) > 0)) expected = candidate;
      }
      var actual = resolver.ResolveMaxScore(hand, incoming, PatternMask.All);
      Assert.IsNotNull(expected);
      Assert.IsNotNull(actual);
      Assert.AreEqual(0, expected.CompareTo(actual));
      Assert.IsTrue(hand.freeTiles.Any(t => t.tile.IsWildcard) || incoming.tile.IsWildcard);
    }

    [DataTestMethod]
    [DataRow("111m222p33s44z5s**", "6s", ScoringOption.Default)]
    [DataRow("111m222p333s44z5s*", "*", ScoringOption.Default)]
    [DataRow("11223344z556s**", "6s", ScoringOption.Default)]
    [DataRow("19m19p19s12345z**", "6z", ScoringOption.Default)]
    [DataRow("111m222p33s44z5s**", "6s", ScoringOption.Yakuman | ScoringOption.KazoeYakuman)]
    [DataRow("111m222p333s44z5s*", "*", ScoringOption.Aotenjou)]
    [DataRow("11223344z556s**", "6s", ScoringOption.Yakuman | ScoringOption.KazoeYakuman)]
    [DataRow("19m19p19s12345z**", "6z", ScoringOption.Aotenjou)]
    public void TwoWildcardSearchMatchesCompleteSubstitutionOracle(string freeTiles, string winningTile,
        ScoringOption option) {
      var game = WildcardFixture.Game(true);
      game.config.scoringOption = option;
      var hand = WildcardFixture.Hand(game, freeTiles);
      var incoming = WildcardFixture.Incoming(winningTile, false);
      var resolver = game.Get<PatternResolver>();
      var free = hand.freeTiles.Select(t => new GameTile(t.tile, t.traceId)).ToList();
      var cloneIncoming = WildcardFixture.Incoming(winningTile, false);
      var wild = free.Append(cloneIncoming).Where(t => t.tile.IsWildcard).ToArray();
      Assert.AreEqual(2, wild.Length);
      ScoreStorage best = null;
      foreach (var a in Tiles.AllDistinct) foreach (var b in Tiles.AllDistinct) {
        wild[0].tile = a; wild[1].tile = b;
        var score = resolver.ResolveMaxScoreLegacy(hand.WithFreeTiles(free), cloneIncoming, PatternMask.All);
        if (score != null && (best == null || score.CompareTo(best) > 0)) best = score;
      }
      var actual = resolver.ResolveMaxScore(hand, incoming, PatternMask.All);
      Assert.IsNotNull(best);
      Assert.IsNotNull(actual);
      Assert.AreEqual(0, best.CompareTo(actual), $"{freeTiles} + {winningTile}");
    }

    [TestMethod]
    public void NaturalCalledMeldsRemainFixedAndBonusCannotQualifyAWin() {
      var game = WildcardFixture.Game(true);
      var hand = WildcardFixture.Hand(game, "111m222p33s4z*");
      var kanTiles = new Tiles("5555z").ToGameTileList();
      kanTiles[0].discardInfo = new(game.players[1], DiscardReason.Draw, 0);
      hand.called = [new Kan(kanTiles)];
      hand.menzen = false;
      var incoming = WildcardFixture.Incoming("4z", false);
      var resolver = game.Get<PatternResolver>();
      ScoreStorage best = null;
      foreach (var tile in Tiles.AllDistinct) {
        var free = hand.freeTiles.Select(t => new GameTile(t.tile.IsWildcard ? tile : t.tile, t.traceId)).ToList();
        var score = resolver.ResolveMaxScoreLegacy(hand.WithFreeTiles(free), incoming, PatternMask.All);
        if (score != null && (best == null || score > best)) best = score;
      }
      var actual = resolver.ResolveMaxScore(hand, incoming, PatternMask.All);
      Assert.IsNotNull(best);
      Assert.IsNotNull(actual);
      Assert.AreEqual(0, best.CompareTo(actual));
      var fixedGroup = actual.resolution.Groups.Single(g => g.Kind == CompactMeldKind.Quad);
      CollectionAssert.AreEqual(kanTiles.ToArray(), fixedGroup.Tiles.Select(t => t.Physical).ToArray());
      Assert.IsNull(resolver.ResolveBestValidScore(hand, incoming, PatternMask.Bonus, 1));
    }

    [DataTestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void FirstDrawDisconnectedFamilyMatchesSubstitutionOracle(bool firstDraw, bool tsumo) {
      var game = WildcardFixture.Game(true);
      if (firstDraw) foreach (var player in game.players) player.hand.jun = 1;
      var hand = WildcardFixture.Hand(game, "147m147p147s123z*");
      var incoming = WildcardFixture.Incoming("4z", tsumo);
      var resolver = game.Get<PatternResolver>();
      ScoreStorage best = null;
      foreach (var tile in Tiles.AllDistinct) {
        var free = hand.freeTiles.Select(t => new GameTile(t.tile.IsWildcard ? tile : t.tile, t.traceId)).ToList();
        var score = resolver.ResolveMaxScoreLegacy(hand.WithFreeTiles(free), incoming, PatternMask.All);
        if (score != null && (best == null || score > best)) best = score;
      }
      var actual = resolver.ResolveMaxScore(hand, incoming, PatternMask.All);
      if (best == null) Assert.IsNull(actual);
      else {
        Assert.IsNotNull(actual);
        Assert.AreEqual(0, best.CompareTo(actual));
        Assert.IsTrue(actual.resolution.Groups.Any(g => g.Kind == CompactMeldKind.Single));
      }
    }

    [TestMethod]
    [DoNotParallelize]
    // ~0.2s uninstrumented; coverage of the bound loop makes it ~2.5s on Release, ~6s on Debug.
    [Timeout(20_000)]
    public void AllWildcardsWithKoyakuAndAllWaitsRemainBounded() {
      var game = WildcardFixture.Game(true);
      var hand = WildcardFixture.Hand(game, new string('*', 13));
      var resolver = game.Get<PatternResolver>();
      var clock = Stopwatch.StartNew();
      var batch = resolver.ResolveScorePreviews(hand, hand.Tenpai, PatternMask.All, PatternMask.Luck);
      Assert.AreEqual(35, batch.Count);
      Assert.AreEqual(7, batch[Tile.Wildcard].Ron.result.yakuman);
      Assert.AreEqual(7, batch[Tile.Wildcard].Tsumo.result.yakuman);
      Assert.IsTrue(batch[Tile.Wildcard].Ron.resolution.VisitedTargets < 1000000);
      foreach (var pair in batch) {
        Assert.IsNotNull(pair.Value.Ron);
        Assert.IsNotNull(pair.Value.Tsumo);
      }
      Console.WriteLine($"all-wild 35-wait batch: {clock.Elapsed.TotalMilliseconds:F2} ms; targets={batch[Tile.Wildcard].Ron.resolution.VisitedTargets}");
    }

    [DataTestMethod]
    [DataRow(false, 19)]
    [DataRow(true, 17)]
    [DoNotParallelize]
    [Timeout(20_000)]
    public void SingleYakumanCapStillMaximizesHanWithManyWildcards(bool tsumo, int han) {
      var game = WildcardFixture.Game(true);
      game.config.scoringOption &= ~ScoringOption.MultipleYakuman;
      // Retains both global maxima but avoids spending coverage-test time on
      // unrelated all-suited targets. The fully wild case is benchmarked too.
      var hand = WildcardFixture.Hand(game, "111111z*******");
      var score = game.Get<PatternResolver>().ResolveBestValidScore(hand,
          WildcardFixture.Incoming("*", tsumo), PatternMask.All, 1, PatternMask.Luck);
      Assert.IsNotNull(score);
      Assert.AreEqual(8000L, score.result.BaseScore);
      Assert.AreEqual(han, score.result.han);
      Assert.IsTrue(score.resolution.VisitedTargets < 20000);
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(10)]
    [DataRow(11)]
    [DataRow(12)]
    [DataRow(13)]
    [DataRow(14)]
    public void EveryWildcardCountProducesAConsistentWinningWitness(int w) {
      var game = WildcardFixture.Game();
      var resolver = game.Get<PatternResolver>();
      var original = new Tiles("123m456p789s111z22z");
      var full = new Tiles(original.Skip(w).Concat(Enumerable.Repeat(Tile.Wildcard, w)));
      var hand = WildcardFixture.Hand(game, new Tiles(full.Take(13)).ToString());
      var incoming = WildcardFixture.Incoming(full[13].ToString(), w % 2 == 0);
      var result = resolver.ResolveMaxScore(hand, incoming, PatternMask.All);
      Assert.IsNotNull(result, $"W={w}");
      var tiles = result.resolution.Groups.SelectMany(g => g.Tiles).ToArray();
      Assert.AreEqual(14, tiles.Length);
      Assert.AreEqual(14, tiles.Select(t => t.Physical).Distinct().Count());
      Assert.AreEqual(w, tiles.Count(t => t.Physical.tile.IsWildcard));
      Assert.AreEqual(1, tiles.Count(t => t.Physical == incoming));
    }
  }
}
