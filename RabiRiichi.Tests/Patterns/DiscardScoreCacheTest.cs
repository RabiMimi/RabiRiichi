using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns;
using RabiRiichi.Patterns.Compiled;
using RabiRiichi.Tests.Helper;
using System.Linq;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class DiscardScoreCacheTest {
    private static void SameScore(ScoreStorage expected, ScoreStorage actual, string label) {
      if (expected == null) { Assert.IsNull(actual, label); return; }
      Assert.IsNotNull(actual, label);
      Assert.AreEqual(0, expected.CompareTo(actual), label);
      Assert.AreEqual(expected.result.yaku, actual.result.yaku, label + " yaku");
      Assert.AreEqual(expected.result.yakuman, actual.result.yakuman, label + " yakuman");
      Assert.AreEqual(expected.result.bonusYakuman, actual.result.bonusYakuman, label + " bonus yakuman");
    }

    [DataTestMethod]
    [DataRow(ScoringOption.Aotenjou)]
    [DataRow(ScoringOption.Default)]
    [DataRow(ScoringOption.Yakuman | ScoringOption.KazoeYakuman)]
    [Timeout(20000)]
    public void DistinctDiscardsShareScoresWithoutSharingPhysicalBindings(ScoringOption option) {
      var game = WildcardFixture.Game(true);
      game.config.scoringOption = option;
      var hand = WildcardFixture.Hand(game, "123m406p77s*****");
      hand.riichiTile = new(new Tile("9s"), 102);
      game.wall.doras.Clear(); game.wall.doras.Add(new(new Tile("4p"), 100));
      game.wall.uradoras.Clear(); game.wall.uradoras.Add(new(Tile.Wildcard, 101));
      game.wall.revealedDoraCount = 1;
      var incoming = WildcardFixture.Incoming("*");
      var all = hand.freeTiles.Append(incoming).ToList();
      var resolver = game.Get<PatternResolver>();
      var shared = resolver.CreateDiscardScoreCache();
      foreach (var discard in all.DistinctBy(t => t.tile)) {
        var analysis = hand.WithFreeTiles(all.Where(t => t != discard).ToList());
        var waits = analysis.Tenpai;
        var expected = resolver.ResolveScorePreviews(analysis, waits, PatternMask.All, PatternMask.Luck);
        var actual = resolver.ResolveScorePreviews(analysis, waits, PatternMask.All, PatternMask.Luck, shared);
        foreach (var wait in waits) {
          SameScore(expected[wait].Ron, actual[wait].Ron, discard.tile + "/" + wait + "/ron");
          SameScore(expected[wait].Tsumo, actual[wait].Tsumo, discard.tile + "/" + wait + "/tsumo");
          foreach (var score in new[] { actual[wait].Ron, actual[wait].Tsumo }) {
            if (score == null) continue;
            var physical = score.resolution.Groups.SelectMany(g => g.Tiles).Select(t => t.Physical).ToArray();
            Assert.AreEqual(14, physical.Distinct().Count());
            Assert.IsFalse(physical.Contains(discard));
            Assert.IsTrue(analysis.freeTiles.All(physical.Contains));
            Assert.AreEqual(1, physical.Count(t => t == score.resolution.Incoming.Physical));
          }
        }
      }
      Assert.IsTrue(shared.Hits > 0, "The fixture must exercise reused scoring");
      Assert.IsTrue(shared.VisitedNodes > 0 && shared.BoundCalculations > 0);
      Assert.AreEqual(13, hand.freeTiles.Count);
    }

    [TestMethod]
    [Timeout(20000)]
    public void CustomYakuReevaluateThePhysicalHoldingAcrossDiscards() {
      var game = WildcardFixture.Game();
      game.config.scoringOption = ScoringOption.Aotenjou;
      var hand = WildcardFixture.Hand(game, "123m456p77s*****");
      var all = hand.freeTiles.Append(WildcardFixture.Incoming("*")).ToList();
      var resolver = game.Get<PatternResolver>();
      resolver.RegisterStdPatterns(new RuleYaku("SixPhysicalWildcards", resolver.basePatterns)
          .When(Cond.Custom(w => w.Hand.freeTiles.Count(t => t.tile.IsWildcard) == 6)).Han(20));
      var shared = resolver.CreateDiscardScoreCache();
      Assert.IsFalse(shared.ReuseScores);
      foreach (var discard in new[] { all[0], all[^1] }) {
        var analysis = hand.WithFreeTiles(all.Where(t => t != discard).ToList());
        var waits = analysis.Tenpai;
        var expected = resolver.ResolveScorePreviews(analysis, waits, PatternMask.All, PatternMask.Luck);
        var actual = resolver.ResolveScorePreviews(analysis, waits, PatternMask.All, PatternMask.Luck, shared);
        foreach (var wait in waits) {
          SameScore(expected[wait].Ron, actual[wait].Ron, wait.ToString());
          SameScore(expected[wait].Tsumo, actual[wait].Tsumo, wait.ToString());
        }
      }
      Assert.AreEqual(0L, shared.Hits);
    }

    [TestMethod]
    [Timeout(20000)]
    public void DiscardActionMatchesIndependentPreviewsAndStartsANewCacheEveryTime() {
      var game = WildcardFixture.Game(true);
      game.config.scoringOption = ScoringOption.Aotenjou;
      var hand = WildcardFixture.Hand(game, "123m11z********");
      var incoming = WildcardFixture.Incoming("*");
      var all = hand.freeTiles.Append(incoming).ToList();
      var resolver = game.Get<PatternResolver>();
      var candidates = PlayTileAction.ComputeDiscardCandidates(hand.player, all, incoming, out var shared);
      Assert.IsNotNull(shared);
      foreach (var candidate in candidates.DistinctBy(c => c.tile.tile)) {
        var analysis = hand.WithFreeTiles(all.Where(t => t != candidate.tile).ToList());
        var expected = PlayTileAction.ComputeTenpaiInfos(resolver, analysis, analysis.Tenpai);
        CollectionAssert.AreEqual(expected.Select(Describe).ToArray(), candidate.tenpaiInfos.Select(Describe).ToArray());
      }
      game.wall.doras.Clear(); game.wall.doras.Add(new(Tile.Wildcard, 100)); game.wall.revealedDoraCount = 1;
      PlayTileAction.ComputeDiscardCandidates(hand.player, all, incoming, out var next);
      Assert.AreNotSame(shared, next);
      Assert.IsTrue(shared.Hits > 0);
    }

    private static string Describe(TenpaiInfo i) => $"{i.winningTile}/{i.points}/{i.han}/{i.fu}/{i.yaku}/{i.yakuman}/{i.bonusYakuman}/{i.maxHan}";

    [TestMethod]
    [Timeout(20000)]
    public void OpenCallsAndQuadsStayFixedWhenDiscardScoresAreShared() {
      var game = WildcardFixture.Game(true);
      game.config.scoringOption = ScoringOption.Aotenjou;
      var hand = WildcardFixture.Hand(game, "123m****");
      var pon = new Tiles("111z").ToGameTileList();
      pon[0].source = TileSource.Discard;
      pon[0].discardInfo = new(game.players[1], DiscardReason.Draw, 0);
      hand.called = [new Kou(pon), new Kan(new Tiles("5555z").ToGameTileList())];
      hand.menzen = false;
      var all = hand.freeTiles.Append(WildcardFixture.Incoming("0p")).ToList();
      var resolver = game.Get<PatternResolver>();
      var shared = resolver.CreateDiscardScoreCache();
      foreach (var discard in all.DistinctBy(t => t.tile)) {
        var analysis = hand.WithFreeTiles(all.Where(t => t != discard).ToList());
        var waits = analysis.Tenpai;
        var expected = resolver.ResolveScorePreviews(analysis, waits, PatternMask.All, PatternMask.Luck);
        var actual = resolver.ResolveScorePreviews(analysis, waits, PatternMask.All, PatternMask.Luck, shared);
        foreach (var wait in waits) {
          SameScore(expected[wait].Ron, actual[wait].Ron, wait.ToString());
          SameScore(expected[wait].Tsumo, actual[wait].Tsumo, wait.ToString());
          foreach (var score in new[] { actual[wait].Ron, actual[wait].Tsumo }) {
            if (score == null) continue;
            for (int i = 0; i < hand.called.Count; i++)
              CollectionAssert.AreEqual(hand.called[i].ToArray(), score.resolution.Groups[i].Tiles.Select(t => t.Physical).ToArray());
          }
        }
      }
      Assert.IsTrue(shared.Hits > 0);
    }

    [TestMethod]
    public void CanonicalCacheKeyKeepsWinningRoleCalledAndClosedGroupsDistinct() {
      CompactMeld pair = new(CompactMeldKind.Pair, 4, true);
      CompactMeld triplet = new(CompactMeldKind.Triplet, 4, true);
      Assert.AreEqual(DiscardScoreCache.Key([pair, triplet], 0), DiscardScoreCache.Key([triplet, pair], 1));
      Assert.AreNotEqual(DiscardScoreCache.Key([pair, triplet], 0), DiscardScoreCache.Key([pair, triplet], 1));
      Assert.AreNotEqual(DiscardScoreCache.Key([pair, triplet]), DiscardScoreCache.Key([pair, triplet with { IsCalled = true }]));
      Assert.AreNotEqual(DiscardScoreCache.Key([pair, triplet]), DiscardScoreCache.Key([pair, triplet with { IsClose = false }]));
      var singles = Enumerable.Range(0, 13).Select(i => new CompactMeld(CompactMeldKind.Single, i, true)).ToArray();
      Assert.AreNotEqual(DiscardScoreCache.Key(singles, 11), DiscardScoreCache.Key(singles, 12));
    }
  }
}


