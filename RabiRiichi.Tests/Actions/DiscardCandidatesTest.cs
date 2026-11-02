using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Actions.Resolver;
using RabiRiichi.Core;
using RabiRiichi.Patterns;
using RabiRiichi.Patterns.Compiled;
using RabiRiichi.Tests.Helper;
using RabiRiichi.Tests.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Tests.Actions {
  [TestClass]
  public class DiscardCandidatesTest {
    private static Game Table(bool wildDeck) {
      var game = WildcardFixture.Game();
      if (wildDeck) game.config.initialTiles = new Tiles(game.config.initialTiles) { Tile.Wildcard };
      return game;
    }

    /// <summary>Mixes winning shapes, near misses and random deals, so every outcome occurs.</summary>
    private static Tiles Deal(Random random, int wildcards) {
      var counts = new int[34];
      var tiles = new Tiles();
      void Add(int i) { counts[i]++; tiles.Add(CompactMeld.TileAt(i)); }
      if (random.Next(3) == 0) {
        while (tiles.Count < 14) Add(random.Next(34));
      } else {
        for (int meld = 0; meld < 4; meld++) {
          int start = random.Next(34);
          bool sequence = start < 27 && start % 9 < 7 && random.Next(3) > 0;
          for (int n = 0; n < 3; n++) Add(start + (sequence ? n : 0));
        }
        int pair = random.Next(34);
        Add(pair); Add(pair);
        if (random.Next(2) == 0) tiles[random.Next(14)] = CompactMeld.TileAt(random.Next(34));
      }
      // A custom wall may hold a red copy of any tile.
      if (random.Next(2) == 0) {
        int red = random.Next(14);
        tiles[red] = new Tile(tiles[red].Suit, tiles[red].Num, true);
      }
      for (int n = 0; n < wildcards; n++) tiles[random.Next(14)] = Tile.Wildcard;
      return tiles;
    }

    private static string Describe(IEnumerable<TenpaiInfo> infos) => string.Join(" ", infos.Select(
        i => $"{i.winningTile}:{i.han}/{i.yaku}/{i.fu}/{i.yakuman}/{i.bonusYakuman}/{i.points}/{i.maxHan}"));

    [DataTestMethod]
    [DataRow(false, 0)]
    [DataRow(true, 0)]
    [DataRow(true, 1)]
    [DataRow(true, 2)]
    [DataRow(true, 3)]
    [DataRow(true, 5)]
    [Timeout(60_000)]
    public void SkippingNonTenpaiDiscardsMatchesCheckingEachOne(bool wildDeck, int wildcards) {
      var game = Table(wildDeck);
      var player = game.players[0];
      var resolver = game.Get<PatternResolver>();
      var random = new Random(4242 + wildcards);
      for (int sample = 0; sample < Math.Max(3, 60 / (1 + wildcards * wildcards)); sample++) {
        var tiles = Deal(random, wildcards);
        var hand = WildcardFixture.Hand(game, new Tiles(tiles.Take(13)).ToString());
        var incoming = WildcardFixture.Incoming(tiles[13].ToString());
        var options = hand.freeTiles.Append(incoming).ToList();
        var candidates = PlayTileAction.ComputeDiscardCandidates(player, options, incoming);
        Assert.AreEqual(options.Count, candidates.Count);
        foreach (var candidate in candidates) {
          var rest = options.Where(t => !ReferenceEquals(t, candidate.tile)).ToList();
          var holding = hand.WithFreeTiles(rest);
          var expected = resolver.ResolveShanten(holding, null, out var waits, 0) == 0
              ? PlayTileAction.ComputeTenpaiInfos(resolver, holding, waits) : [];
          Assert.AreEqual(Describe(expected), Describe(candidate.tenpaiInfos), $"{tiles} discarding {candidate.tile.tile}");
        }
      }
    }

    [TestMethod]
    public void TenpaiDiscardsAreOnlyNamedWhenAOneQueryAnswerExists() {
      var game = WildcardFixture.Game();
      var resolver = game.Get<PatternResolver>();
      Tiles Discards(string tiles) => resolver.ResolveTenpaiDiscards(WildcardFixture.Hand(game, tiles));
      // Complete hands have no surplus tile, so every discard needs its own check.
      Assert.IsNull(Discards("123m456p789s11122z"));
      Assert.AreEqual(0, Discards("147m258p369s12345z").Count);
      CollectionAssert.AreEqual(new Tiles("25z"), Discards("123m456p789s11125z"));
      // A hand of the wrong size, as tests and custom callers may build, falls back too.
      Assert.IsNull(Discards("123m456p789s1112z"));
    }

    [TestMethod]
    public void CustomBasePatternsAlwaysCheckEachDiscard() {
      var game = WildcardFixture.Game();
      var resolver = game.Get<PatternResolver>();
      resolver.RegisterBasePatterns(new NeverWinningFamily());
      Assert.IsNull(resolver.ResolveTenpaiDiscards(WildcardFixture.Hand(game, "147m258p369s12345z")));
    }

    private sealed class NeverWinningFamily : BasePattern {
      public override bool Resolve(Hand hand, GameTile incoming, out List<List<MenLike>> output) {
        output = null;
        return false;
      }
      public override int Shanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten = int.MaxValue) =>
          Reject(out output);
    }

    [DataTestMethod]
    [DataRow("123m456p789s1112z", "")]
    [DataRow("22334455667r5m7m", "")]
    [DataRow("1112223334445z", "")]
    [DataRow("19m19p19s1234567z", "")]
    [DataRow("123m456p11s", "789s")]
    [DataRow("11m55p77s", "111z,222z")]
    public void NaturalPreviewsMatchScoringEachSourceAlone(string tiles, string called) {
      var game = WildcardFixture.Game();
      var hand = WildcardFixture.Hand(game, tiles);
      if (called != "") {
        hand.called = called.Split(',').Select(g => {
          var group = new Tiles(g).ToGameTileList();
          group[0].discardInfo = new DiscardInfo(null, Generated.Core.DiscardReason.Draw, 0);
          group[0].source = Generated.Core.TileSource.Discard;
          return MenLike.From(group);
        }).ToList();
        hand.menzen = false;
      }
      var resolver = game.Get<PatternResolver>();
      var waits = Tiles.AllDistinct;
      var previews = resolver.ResolveScorePreviews(hand, waits, PatternMask.All, PatternMask.Luck);
      foreach (var wait in waits) {
        WildcardFixture.Same(resolver.ResolveMaxScore(hand, WildcardFixture.Incoming(wait.ToString(), false), PatternMask.All, PatternMask.Luck),
            previews[wait].Ron, $"{tiles} ron {wait}");
        WildcardFixture.Same(resolver.ResolveMaxScore(hand, WildcardFixture.Incoming(wait.ToString(), true), PatternMask.All, PatternMask.Luck),
            previews[wait].Tsumo, $"{tiles} tsumo {wait}");
      }
    }

    [TestMethod]
    public void RiichiReusesThePlayActionsPreviews() {
      var game = WildcardFixture.Game();
      var player = game.players[0];
      var hand = WildcardFixture.Hand(game, "123m456p789s1112z");
      var incoming = WildcardFixture.Incoming("9m");
      var inquiry = new MultiPlayerInquiry(game);
      Assert.IsTrue(game.Get<PlayTileResolver>().Resolve(player, incoming, inquiry));
      Assert.IsTrue(game.Get<RiichiResolver>().Resolve(player, incoming, inquiry));
      var play = inquiry.GetByPlayerId(0).actions.OfType<PlayTileAction>().Single(a => a is not RiichiAction);
      var riichi = inquiry.GetByPlayerId(0).actions.OfType<RiichiAction>().Single();
      var fresh = PlayTileAction.ComputeDiscardCandidates(player, riichi.options.Select(o => o.tile).ToList(), incoming);
      Assert.AreEqual(fresh.Count, riichi.candidates.Count);
      for (int i = 0; i < fresh.Count; i++) {
        Assert.AreSame(fresh[i].tile, riichi.candidates[i].tile);
        Assert.AreEqual(Describe(fresh[i].tenpaiInfos), Describe(riichi.candidates[i].tenpaiInfos));
        var shared = play.candidates.Single(c => ReferenceEquals(c.tile, riichi.candidates[i].tile));
        Assert.AreNotSame(shared.tenpaiInfos, riichi.candidates[i].tenpaiInfos);
      }
      Assert.IsTrue(riichi.candidates.Any(c => c.tenpaiInfos.Count > 0));
      Assert.IsTrue(hand.freeTiles.Count == 13);
    }

    [TestMethod]
    public void RiichiWithoutAPlayActionStillPreviewsItsDiscards() {
      var game = WildcardFixture.Game();
      WildcardFixture.Hand(game, "123m456p789s1112z");
      var incoming = WildcardFixture.Incoming("9m");
      var inquiry = new MultiPlayerInquiry(game);
      Assert.IsTrue(game.Get<RiichiResolver>().Resolve(game.players[0], incoming, inquiry));
      var riichi = inquiry.GetByPlayerId(0).actions.OfType<RiichiAction>().Single();
      Assert.IsTrue(riichi.candidates.Any(c => c.tenpaiInfos.Count > 0));
    }

    private static Tile[] RiichiDiscards(string tiles, string draw) {
      var game = WildcardFixture.Game();
      WildcardFixture.Hand(game, tiles);
      var inquiry = new MultiPlayerInquiry(game);
      Assert.IsTrue(game.Get<RiichiResolver>().Resolve(game.players[0], WildcardFixture.Incoming(draw), inquiry));
      var riichi = inquiry.GetByPlayerId(0).actions.OfType<RiichiAction>().Single();
      return riichi.options.Select(o => o.tile.tile).ToArray();
    }

    [DataTestMethod]
    [DataRow("1133m2277p4466sr5m", "8s", "r5m8s")]
    [DataRow("1133m2277p4466sr3p", "8s", "r3p8s")]
    [DataRow("1133m2277p4466sr7z", "8s", "r7z8s")]
    [DataRow("19m19p19s123456zr3p", "7z", "r3p")]
    [DataRow("123m456m789p111sr3p", "5z", "r3p5z")]
    public void TenpaiRiichiCanDiscardARedTile(string tiles, string draw, string expected) {
      CollectionAssert.AreEquivalent(new Tiles(expected), RiichiDiscards(tiles, draw));
    }

    [DataTestMethod]
    [DataRow("123m34mr5m789p111s9s", "9s")]
    [DataRow("123m456m12pr3p111s9s", "9s")]
    [DataRow("r1m23m456m789p111s9s", "9s")]
    [DataRow("123m456m789p111sr7z", "r7z")]
    [DataRow("123m456m12p111s9s9s", "r3p")]
    public void CompleteHandRiichiCanDiscardAnyTileIncludingARedOne(string tiles, string draw) {
      var expected = new Tiles(tiles) { new Tile(draw) };
      Assert.IsTrue(expected.Any(t => t.Akadora));
      CollectionAssert.AreEquivalent(expected, RiichiDiscards(tiles, draw));
    }
  }
}