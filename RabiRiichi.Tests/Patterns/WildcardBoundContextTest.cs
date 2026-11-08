using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns;
using RabiRiichi.Patterns.Compiled;
using RabiRiichi.Tests.Helper;
using System.Linq;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class WildcardBoundContextTest {
    [DataTestMethod]
    [DataRow(false, false, false, 0)]
    [DataRow(true, false, false, 0)]
    [DataRow(false, true, false, 0)]
    [DataRow(true, true, false, 0)]
    [DataRow(false, false, true, 0)]
    [DataRow(false, false, true, 1)]
    [Timeout(20000)]
    public void MixedWinContextsMatchIndependentSearches(bool open, bool custom, bool firstDraw, int seat) {
      var game = WildcardFixture.Game(true);
      game.config.scoringOption = ScoringOption.Aotenjou;
      var hand = game.players[seat].hand;
      hand.freeTiles = new Tiles(open ? "456m77p*****" : "123456m77p*****").ToGameTileList();
      if (firstDraw) {
        foreach (var player in game.players) player.hand.jun = 1;
        hand.jun = seat == 0 ? 1 : 0;
      }
      if (open) {
        var pon = new Tiles("111z").ToGameTileList();
        pon[0].source = TileSource.Discard;
        pon[0].discardInfo = new(game.players[1], DiscardReason.Draw, 0);
        hand.called = [new Kou(pon)];
        hand.menzen = false;
      }
      hand.riichiTile = open || firstDraw ? null : new(new Tile("9s"), 100);
      game.wall.remaining.Clear(); // Exercise last-tile source-dependent yaku.
      game.wall.doras.Clear(); game.wall.doras.Add(new(new Tile("4p"), 101));
      game.wall.uradoras.Clear(); game.wall.uradoras.Add(new(Tile.Wildcard, 102));
      game.wall.revealedDoraCount = 1;
      var resolver = game.Get<PatternResolver>();
      if (custom) resolver.RegisterStdPatterns(new RuleYaku("TsumoWithWhiteDragons", resolver.basePatterns)
          .When(Cond.Custom(w => w.IsTsumo && w.TileCount(31) >= 3, p => p.IsTsumo)).Han(30));

      // Same physical incoming identities under several source contexts. Red
      // and ordinary fives share structure but have different physical bonuses.
      var incoming = new[] { "5p", "0p", "1z", "*" }.SelectMany(tile => new[] {
        WildcardFixture.Incoming(tile, false), WildcardFixture.Incoming(tile),
        new GameTile(new Tile(tile), -1) { source = TileSource.Wanpai },
        new GameTile(new Tile(tile), -1) { source = TileSource.Discard,
          discardInfo = new(null, DiscardReason.Chankan, 0) },
        new GameTile(new Tile(tile), -1) { source = TileSource.Discard,
          discardInfo = new(null, DiscardReason.DrawRinshan, 0) },
        new GameTile(new Tile(tile), -1) { source = TileSource.Discard,
          discardInfo = new(null, DiscardReason.Draw, 0, isRiichiDeclaration: true) },
      }).ToArray();
      var batch = new WildcardScoreSearch(resolver, new CompiledScoringEvaluator(resolver.stdPatterns),
          hand, incoming, PatternMask.All, PatternMask.None, 0).Run();
      for (int i = 0; i < incoming.Length; i++) {
        var expected = resolver.ResolveMaxScore(hand, incoming[i], PatternMask.All);
        Assert.IsNotNull(expected);
        Assert.IsNotNull(batch[i]);
        Assert.AreEqual(0, expected.CompareTo(batch[i]), $"context {i}");
        Assert.AreEqual(expected.result.yaku, batch[i].result.yaku, $"context {i} yaku");
        Assert.AreEqual(expected.result.yakuman, batch[i].result.yakuman, $"context {i} yakuman");
        Assert.AreEqual(expected.result.bonusYakuman, batch[i].result.bonusYakuman, $"context {i} bonus yakuman");
      }
      if (custom) Assert.IsTrue(batch.Where((_, i) => incoming[i].IsTsumo)
          .Any(s => s.Any(y => y.Src == "TsumoWithWhiteDragons")), "Exercise the source-dependent custom yaku");
    }
  }
}
