using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Core.Setup;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Helper;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class CompiledScoringTest {
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BuiltInFeatureRulesMatchLegacyScoresAndEntries(bool koyaku) {
      var game = WildcardFixture.Game(koyaku);
      var hand = game.players[0].hand;
      var resolver = game.Get<PatternResolver>();
      var random = new Random(1977);
      string[] named = [
        "11122233344455z", "11111222333444z", "22266667778999s", "11123455678999s",
        "22334455667788p", "11223344556677z", "22233344466688s", "11155577799977s",
        "11122233344455m", "11144477799955m", "11133355599911p", "112233m112233p55s",
        "123456789m111z22p", "123m123p123s111z22s", "19m19p19s11234567z",
        // Hello World's rank histogram across suits: no flush, so no yakuman.
        "222m666p67778999s",
      ];
      for (int sample = 0; sample < named.Length + 100; sample++) {
        var full = sample < named.Length ? new Tiles(named[sample]) : RandomWinning(random);
        hand.called = [];
        int at = random.Next(full.Count);
        var physical = full[at]; full.RemoveAt(at);
        hand.freeTiles = full.ToGameTileList();
        var incoming = WildcardFixture.Incoming(physical.ToString(), sample % 2 == 0);
        hand.riichiTile = sample % 3 == 0 ? new GameTile(new Tile("9m"), 200) : null;
        hand.wRiichi = hand.riichi && sample % 9 == 0;
        hand.ippatsu = hand.riichi && sample % 5 == 0;
        foreach (var option in new[] { ScoringOption.Default, ScoringOption.Aotenjou }) {
          game.config.scoringOption = option;
          foreach (var mask in new[] { PatternMask.All, PatternMask.Regular, PatternMask.Bonus }) {
            var exclude = sample % 4 == 0 ? PatternMask.Luck : PatternMask.None;
            var expected = resolver.ResolveMaxScoreLegacy(hand, incoming, mask, exclude);
            var actual = resolver.ResolveMaxScore(hand, incoming, mask, exclude);
            WildcardFixture.Same(expected, actual, $"{sample}: {full}+{physical}; {option}/{mask}/{exclude}");
          }
        }
      }
    }

    private static Tiles RandomWinning(Random random) {
      var tiles = new Tiles();
      for (int m = 0; m < 4; m++) {
        if (random.Next(2) == 0) {
          var t = HandShapeAnalyzer.FromIndex(random.Next(34));
          tiles.AddRange([t, t, t]);
        } else {
          int i = random.Next(3) * 9 + random.Next(7);
          tiles.AddRange([HandShapeAnalyzer.FromIndex(i), HandShapeAnalyzer.FromIndex(i + 1), HandShapeAnalyzer.FromIndex(i + 2)]);
        }
      }
      var pair = HandShapeAnalyzer.FromIndex(random.Next(34));
      tiles.AddRange([pair, pair]);
      return tiles;
    }

    [TestMethod]
    public void OpenMeldsKansAndPhysicalBonusesMatchLegacy() {
      var game = WildcardFixture.Game(true);
      var hand = WildcardFixture.Hand(game, "111222333m4z");
      hand.called = [new Kan(new Tiles("5555z").ToGameTileList())];
      var incoming = WildcardFixture.Incoming("4z", false);
      var resolver = game.Get<PatternResolver>();
      WildcardFixture.Same(resolver.ResolveMaxScoreLegacy(hand, incoming, PatternMask.All),
          resolver.ResolveMaxScore(hand, incoming, PatternMask.All), "closed kan");
      hand.called[0][0].discardInfo = new DiscardInfo(game.players[1], DiscardReason.Draw, 0);
      hand.called = [new Kan(hand.called[0])];
      hand.menzen = false;
      hand.nukiDora.Add(new(new Tile("4z"), 300));
      game.wall.doras.Clear(); game.wall.doras.Add(new(new Tile("3z"), 301)); game.wall.revealedDoraCount = 1;
      WildcardFixture.Same(resolver.ResolveMaxScoreLegacy(hand, incoming, PatternMask.All),
          resolver.ResolveMaxScore(hand, incoming, PatternMask.All), "open kan and nuki");
    }

    [TestMethod]
    public void DisabledDependenciesRemainTrueWithoutContributingScores() {
      var game = WildcardFixture.Game(true);
      var hand = WildcardFixture.Hand(game, "1122334455667z");
      var resolver = game.Get<PatternResolver>();
      resolver.stdPatterns.RemoveWhere(p => p is Chiitoitsu or Tsuuiisou);
      var incoming = WildcardFixture.Incoming("7z");
      WildcardFixture.Same(resolver.ResolveMaxScoreLegacy(hand, incoming, PatternMask.All),
          resolver.ResolveMaxScore(hand, incoming, PatternMask.All), "frozen dependencies");
    }

    [TestMethod]
    public void UncappedComparisonRemainsExactAfterLongScoreSaturates() {
      var lower = new ScoreCalcResult(ScoringOption.Aotenjou) { fu = 40, han = 100 };
      var higher = new ScoreCalcResult(ScoringOption.Aotenjou) { fu = 30, han = 101 };
      Assert.AreEqual(long.MaxValue, lower.BaseScore);
      Assert.AreEqual(long.MaxValue, higher.BaseScore);
      Assert.IsTrue(higher.CompareTo(lower) > 0);
      var tie = new ScoreCalcResult(ScoringOption.Aotenjou) { fu = 60, han = 100 };
      Assert.IsTrue(higher.CompareTo(tie) > 0, "Equal exact base points retain the han tie-break");
    }

    [TestMethod]
    public void MinimumYakuIsAppliedBeforeMaximizingScore() {
      var game = WildcardFixture.Game();
      var hand = WildcardFixture.Hand(game, "111222333m444p5s");
      var incoming = WildcardFixture.Incoming("5s");
      var resolver = new PatternResolver();
      var family = new Base33332();
      resolver.RegisterBasePatterns(family);
      resolver.RegisterStdPatterns(new TradeoffPattern(family));
      Assert.AreEqual(2, resolver.ResolveMaxScore(hand, incoming, PatternMask.All).result.yaku);
      var valid = resolver.ResolveBestValidScore(hand, incoming, PatternMask.All, 3);
      Assert.IsNotNull(valid);
      Assert.AreEqual(3, valid.result.yaku);
      Assert.AreEqual(20, valid.result.fu);
      Assert.IsNull(resolver.ResolveBestValidScore(hand, incoming, PatternMask.All, 4));
      hand.freeTiles[0].tile = Tile.Wildcard;
      Assert.ThrowsException<NotSupportedException>(() => resolver.ResolveMaxScore(hand, incoming, PatternMask.All));
    }

    [TestMethod]
    public void WildcardTablesRejectRulesWithoutCompiledScoring() {
      var config = new GameConfig { actionCenter = new JsonStringActionCenter(null), setup = new CustomRuleSetup() };
      config.initialTiles = new Tiles(config.initialTiles) { Tile.Wildcard };
      var error = Assert.ThrowsException<NotSupportedException>(() => new Game(config));
      StringAssert.Contains(error.Message, nameof(CustomRule));
      // Natural tables keep the legacy fallback for custom rules.
      config.initialTiles = Tiles.All.Value;
      Assert.IsNotNull(new Game(config));
    }

    private sealed class CustomRuleSetup : RiichiSetup {
      protected override void InitPatterns() {
        base.InitPatterns();
        AddStdPattern<CustomRule>();
      }
    }

    private sealed class CustomRule : StdPattern {
      public CustomRule(Base33332 family) => BaseOn(family);
      public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) => false;
    }

    private sealed class TradeoffPattern : StdPattern {
      internal TradeoffPattern(BasePattern family) => BaseOn(family);
      public override PatternMask type => PatternMask.Regular;
      public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
        bool sequences = groups.Any(g => g is Shun);
        scores.Add(new(ScoringType.Han, sequences ? 3 : 2, this));
        scores.Add(new(ScoringType.Fu, sequences ? 20 : 110, this));
        return true;
      }
    }
  }
}
