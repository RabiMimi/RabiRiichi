using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Actions.Resolver;
using RabiRiichi.Communication.Sync;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Patterns;
using System.Diagnostics;
using System.Linq;

namespace RabiRiichi.Tests.Actions {
  [TestClass]
  public class WildcardActionTest {
    [TestMethod]
    public void RonTsumoAndFuritenUseWildcardInterpretations() {
      var game = WildcardFixture.Game();
      var hand = WildcardFixture.Hand(game, "111m222p333s44z5s*");
      var incoming = WildcardFixture.Incoming("5s", false);
      incoming.discardInfo = new(game.players[1], DiscardReason.Draw, 0);
      var inquiry = new MultiPlayerInquiry(game);
      Assert.IsTrue(game.Get<RonResolver>().Resolve(game.players[1], incoming, inquiry));
      Assert.IsTrue(inquiry.GetByPlayerId(0).actions.Any(a => a is RonAction));
      hand.isDiscardFuriten = true;
      inquiry = new(game);
      Assert.IsFalse(game.Get<RonResolver>().Resolve(game.players[1], incoming, inquiry));
      var tsumo = WildcardFixture.Incoming("5s");
      Assert.IsTrue(game.Get<TsumoResolver>().Resolve(game.players[0], tsumo, new(game)));
    }

    [TestMethod]
    public void WildcardsCannotBeClaimedAsMeldsOrKans() {
      var game = WildcardFixture.Game();
      WildcardFixture.Hand(game, new string('*', 13));
      var incoming = WildcardFixture.Incoming("*", false);
      incoming.discardInfo = new(game.players[1], DiscardReason.Draw, 0);
      Assert.IsFalse(game.Get<PonResolver>().Resolve(game.players[1], incoming, new(game)));
      Assert.IsFalse(game.Get<ChiiResolver>().Resolve(game.players[1], incoming, new(game)));
      Assert.IsFalse(game.Get<KanResolver>().Resolve(game.players[1], incoming, new(game)));
      Assert.IsFalse(game.Get<KanResolver>().Resolve(game.players[0], WildcardFixture.Incoming("*"), new(game)));
    }

    [TestMethod]
    public void ConcealedKanRobbingIsScoredOnlyAsKokushi() {
      var game = WildcardFixture.Game();
      WildcardFixture.Hand(game, new string('*', 13));
      var incoming = WildcardFixture.Incoming("1z", false);
      incoming.discardInfo = new(game.players[1], DiscardReason.Chankan, 0);
      var inquiry = new MultiPlayerInquiry(game);
      Assert.IsTrue(game.Get<ChanAnkanResolver>().Resolve(game.players[1], incoming, inquiry));
      var action = inquiry.GetByPlayerId(0).actions.OfType<RonAction>().Single();
      Assert.IsNotNull(action);
      // Inspect the family-restricted resolver directly: ordinary six-yakuman
      // wildcard interpretations are ineligible for this action.
      var score = game.Get<PatternResolver>().ResolveBestValidScoreForFamily(game.players[0].hand,
          incoming, game.Get<Base13_1>(), PatternMask.All, 1);
      Assert.AreEqual(2, score.result.yakuman);
      Assert.IsTrue(score.Any(s => s.Source is KokushiMusouJuusanmenMachi));
    }

    [TestMethod]
    [DoNotParallelize]
    [Timeout(20_000)]
    public void DiscardPreviewsBatchIdenticalPhysicalTilesAndDoNotMutateHand() {
      var game = WildcardFixture.Game();
      var hand = WildcardFixture.Hand(game, new string('*', 13));
      var incoming = WildcardFixture.Incoming("*");
      hand.pendingTile = incoming;
      var originalList = hand.freeTiles;
      var original = originalList.ToArray();
      var clock = Stopwatch.StartNew();
      var candidates = PlayTileAction.ComputeDiscardCandidates(game.players[0], hand.freeTiles.Append(incoming).ToList(), incoming);
      Assert.AreEqual(14, candidates.Count);
      Assert.IsTrue(candidates.All(c => c.tenpaiInfos.Count == 35));
      Assert.AreSame(originalList, hand.freeTiles);
      CollectionAssert.AreEqual(original, hand.freeTiles.ToArray());
      Assert.AreSame(incoming, hand.pendingTile);
      Assert.AreNotSame(candidates[0].tenpaiInfos, candidates[1].tenpaiInfos);
      Assert.AreNotSame(candidates[0].tenpaiInfos[0], candidates[1].tenpaiInfos[0]);
      Assert.AreEqual(35, new PlayerHandState(hand, 0).ToProto(0).TenpaiWaits.Count);
      System.Console.WriteLine($"all-wild discard inquiry and sync: {clock.Elapsed.TotalMilliseconds:F2} ms");
    }
  }
}
