using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Helper;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class WildcardShapeTest {
    [TestMethod]
    public void EveryWildcardCountHasExactDistanceAndWaits() {
      var complete = new Tiles("123m456p789s111z22z");
      for (int wild = 0; wild <= 14; wild++) {
        var full = new Tiles(complete.Skip(wild).Concat(Enumerable.Repeat(Tile.Wildcard, wild)));
        var hand = TestHelper.CreateHand(new Tiles(full.Take(13)).ToString());
        var incoming = new GameTile(full[13], -1);
        Assert.AreEqual(-1, HandShapeAnalyzer.RegularShanten(hand, incoming, out var discards, -1), $"W={wild}");
        Assert.AreEqual(0, discards.Count);
      }
      var all = TestHelper.CreateHand(new string('*', 13));
      Assert.AreEqual(0, HandShapeAnalyzer.RegularShanten(all, null, out var waits, 0));
      Assert.AreEqual(35, waits.Count);
      Assert.AreEqual(int.MaxValue, HandShapeAnalyzer.RegularShanten(all, null, out var rejected, -1));
      Assert.IsNull(rejected);
      var family = new Base33332();
      Assert.AreEqual(-1, family.Shanten(all, new GameTile(Tile.Wildcard, -1), out _, -1));
      Assert.ThrowsException<NotSupportedException>(() => family.Resolve(all, new GameTile(Tile.Wildcard, -1), out _));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void MasksMatchIndependentFiniteDifferences(int batch) {
      var random = new Random(20261017 + batch);
      for (int sample = batch * 20; sample < (batch + 1) * 20; sample++) {
        var counts = new int[34];
        int wild = sample % 15;
        for (int n = 0; n < 14 - wild; n++) counts[random.Next(34)]++;
        var answer = HandShapeAnalyzer.AnalyzeRegular(counts);
        for (int i = 0; i < 34; i++) {
          if (counts[i] < 14) {
            counts[i]++;
            Assert.AreEqual(HandShapeAnalyzer.AnalyzeRegular(counts).Deficit < answer.Deficit,
                (answer.DrawMask & (1UL << i)) != 0, $"draw {sample}/{i}");
            counts[i]--;
          }
          if (counts[i] > 0) {
            counts[i]--;
            Assert.AreEqual(HandShapeAnalyzer.AnalyzeRegular(counts).Deficit == answer.Deficit,
                (answer.DiscardMask & (1UL << i)) != 0, $"discard {sample}/{i}");
            counts[i]++;
          }
        }
      }
    }

    [TestMethod]
    public void NaturalDistanceMatchesExistingResolverIncludingCustomCopiesAndCalls() {
      var random = new Random(17);
      Tiles Deal(int count) => new(Enumerable.Range(0, count).Select(_ => HandShapeAnalyzer.FromIndex(random.Next(34))));
      void Check(string tiles, GameTile incoming, params string[] called) {
        var hand = TestHelper.CreateHand(tiles, called);
        foreach (int cap in new[] { 0, 1, 2, int.MaxValue }) {
          int expected = LegacyRegularShanten.Shanten(hand, incoming, out var oldTiles, cap);
          int actual = HandShapeAnalyzer.RegularShanten(hand, incoming, out var newTiles, cap);
          Assert.AreEqual(expected, actual, tiles);
          if (expected != int.MaxValue) oldTiles.AssertEquals(newTiles.ToString());
        }
      }
      for (int sample = 0; sample < 100; sample++) {
        Check(Deal(13).ToString(), null);
        Check(Deal(13).ToString(), new GameTile(HandShapeAnalyzer.FromIndex(random.Next(34)), -1));
        Check(Deal(10).ToString(), null, "222p");
        Check(Deal(7).ToString(), new GameTile(HandShapeAnalyzer.FromIndex(random.Next(34)), -1), "1111m", "345s");
        Check(Deal(11).ToString(), null, "77z");
      }
      var repeated = TestHelper.CreateHand("6666666666666z");
      Assert.AreEqual(0, HandShapeAnalyzer.RegularShanten(repeated, null, out var waits, 0));
      waits.AssertEquals("6z");
      var called = TestHelper.CreateHand("1z", "1111m", "222p", "345s", "777z");
      Assert.AreEqual(0, HandShapeAnalyzer.RegularShanten(called, null, out waits, 0));
      waits.AssertEquals("1z");
    }

    [TestMethod]
    public void SpecialFamiliesUseDistinctPairsAndFixedOrphanPair() {
      var pairs = TestHelper.CreateHand("112233445566z*");
      Assert.AreEqual(0, new Base72().Shanten(pairs, null, out var pairWaits, 0));
      Assert.IsTrue(pairWaits.Contains(new Tile("7z")));
      Assert.IsTrue(pairWaits.Contains(Tile.Wildcard));
      Assert.IsFalse(pairWaits.Contains(new Tile("1z")));
      var kokushi = TestHelper.CreateHand("19m19p19s12345z**");
      Assert.AreEqual(0, new Base13_1().Shanten(kokushi, null, out var orphanWaits, 0));
      Assert.IsTrue(orphanWaits.Contains(Tile.Wildcard));
      Assert.IsFalse(orphanWaits.Contains(new Tile("5s")));
      var fixedPair = TestHelper.CreateHand("19m19p19s2345z*", "11z");
      Assert.AreEqual(0, new Base13_1().Shanten(fixedPair, null, out orphanWaits, 0));
      orphanWaits.AssertEquals("67z*");
    }

    [TestMethod]
    public void SuitAnswersAreIndependentOfTheOrderTheyWereFirstCached() {
      // The same counts must agree whichever hand filled the shared suit cache.
      var counts = new int[34];
      counts[0] = counts[1] = counts[2] = counts[9] = counts[10] = 1;
      var first = HandShapeAnalyzer.AnalyzeRegular(counts);
      var again = HandShapeAnalyzer.AnalyzeRegular(counts);
      Assert.AreEqual(first, again);
      Assert.AreEqual(first, HandShapeAnalyzer.AnalyzeRegular(counts.ToArray()));
    }

    [TestMethod]
    public void WildcardWaitAppearsInConfiguredWallEvenWhenHandHasNone() {
      var game = WildcardFixture.Game();
      var hand = WildcardFixture.Hand(game, "123m456p789s111z2z");
      var resolver = game.Get<PatternResolver>();
      Assert.AreEqual(0, resolver.ResolveShanten(hand, null, out var ordinary, 0));
      Assert.IsFalse(ordinary.Contains(Tile.Wildcard));
      game.config.initialTiles = new Tiles(game.config.initialTiles) { Tile.Wildcard };
      Assert.AreEqual(0, resolver.ResolveShanten(hand, null, out var wild, 0));
      Assert.IsTrue(wild.Contains(Tile.Wildcard));
    }

    [TestMethod]
    public void AnalyzerValidatesItsPublicInputs() {
      Assert.ThrowsException<ArgumentException>(() => HandShapeAnalyzer.AnalyzeRegular(new int[33]));
      var bad = new int[34]; bad[0] = -1;
      Assert.ThrowsException<ArgumentOutOfRangeException>(() => HandShapeAnalyzer.AnalyzeRegular(bad));
      Assert.ThrowsException<ArgumentOutOfRangeException>(() => HandShapeAnalyzer.AnalyzeRegular(new int[34], 5));
      Assert.ThrowsException<ArgumentException>(() => HandShapeAnalyzer.ToIndex(Tile.Wildcard));
      Assert.ThrowsException<ArgumentOutOfRangeException>(() => HandShapeAnalyzer.FromIndex(34));
    }
  }
}
