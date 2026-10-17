using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Helper;
using System.Collections.Generic;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class PatternResolverTest {
    private readonly Base33332 basePattern = new();

    private PatternResolver Resolver(params StdPattern[] patterns) {
      var resolver = new PatternResolver();
      resolver.RegisterBasePatterns(basePattern);
      resolver.RegisterStdPatterns(patterns);
      return resolver;
    }

    private static Hand WinningHand() {
      var hand = TestHelper.CreateHand("123m456p789s111z2z");
      hand.player = TestHelper.CreateGame().GetPlayer(0);
      return hand;
    }

    [DataTestMethod]
    [DataRow(PatternMask.Regular, 1)]
    [DataRow(PatternMask.Bonus, 1)]
    [DataRow(PatternMask.Regular | PatternMask.Bonus, 1)]
    [DataRow(PatternMask.Regular | PatternMask.Luck, 0)]
    [DataRow(PatternMask.Bonus | PatternMask.Luck, 0)]
    [DataRow(PatternMask.Luck, 0)]
    [DataRow(PatternMask.All, 0)]
    public void ExcludedLuckTakesPrecedenceOverOtherTags(PatternMask tags, int expectedHan) {
      var resolver = Resolver(new TaggedPattern(basePattern, tags));
      var incoming = new GameTile(new Tile("2z"), 0);

      var scores = resolver.ResolveMaxScore(
          WinningHand(), incoming, PatternMask.All, excludedMask: PatternMask.Luck);

      Assert.IsNotNull(scores);
      Assert.AreEqual(expectedHan, scores.result.han);
    }

    [TestMethod]
    public void ExcludedLuckCannotBeEvaluatedThroughDependencies() {
      var luck = new TaggedPattern(basePattern, PatternMask.Luck);
      var dependent = new TaggedPattern(basePattern, PatternMask.Regular, luck);
      var resolver = Resolver(luck, dependent);
      var hand = WinningHand();
      var incoming = new GameTile(new Tile("2z"), 0);

      Assert.AreEqual(2, resolver.ResolveMaxScore(hand, incoming, PatternMask.All).result.han);

      var scores = resolver.ResolveMaxScore(
          hand, incoming, PatternMask.All, excludedMask: PatternMask.Luck);

      Assert.AreEqual(0, scores.result.han,
          "An excluded dependency must neither award han nor enable a dependent pattern");
    }

    private sealed class TaggedPattern : StdPattern {
      private readonly PatternMask tags;
      public override PatternMask type => tags;

      public TaggedPattern(BasePattern basePattern, PatternMask tags, StdPattern dependency = null) {
        this.tags = tags;
        BaseOn(basePattern);
        if (dependency != null) {
          DependOn(dependency);
        }
      }

      public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
        scores.Add(new Scoring(ScoringType.Han, 1, this));
        return true;
      }
    }
  }
}