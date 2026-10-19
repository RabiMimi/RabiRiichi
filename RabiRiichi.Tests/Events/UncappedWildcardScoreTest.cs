using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events;
using RabiRiichi.Events.InGame;
using RabiRiichi.Events.InGame.Listener;
using RabiRiichi.Generated.Events.InGame;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Patterns;
using RabiRiichi.Utils;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Events {
  [TestClass]
  public class UncappedWildcardScoreTest {
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LargeUncappedPaymentsDoNotWrap(bool tsumo) {
      var game = WildcardFixture.Game();
      var score = new ScoreStorage { result = new(ScoringOption.Aotenjou) { han = 100, yaku = 100, fu = 40 } };
      var incoming = WildcardFixture.Incoming("*", tsumo);
      int from = tsumo ? 0 : 1;
      var calc = new CalcScoreEvent(new InitGameEvent(game), new AgariInfoList(from, incoming, tsumo, new AgariInfo(0, score)));
      game.players[0].hand.agariTile = incoming;
      await CalcScoreListener.CalcScore(calc);
      Assert.IsTrue(calc.scoreChange.Where(t => t.reason is ScoreTransferReason.Ron or ScoreTransferReason.Tsumo)
          .All(t => t.points == long.MaxValue));
      await ApplyScoreListener.ApplyScore(new ApplyScoreEvent(calc, calc.scoreChange));
      Assert.AreEqual(long.MaxValue, game.players[0].points);
      Assert.IsTrue(game.players[1].points < 0);
      Assert.AreEqual(0, game.PlayersByRank[0].id);
    }

    [TestMethod]
    public void TransfersAccumulateBeforeSaturatingAndRoundSafely() {
      var transfers = new ScoreTransferList {
        new(1, 0, long.MaxValue, ScoreTransferReason.Ron),
        new(2, 0, long.MaxValue, ScoreTransferReason.Ron),
        new(0, 3, long.MaxValue, ScoreTransferReason.Ron),
      };
      Assert.AreEqual(long.MaxValue, transfers.DeltaScore(0));
      Assert.AreEqual(long.MaxValue - 100, transfers.ApplyTo(0, -100));
      Assert.AreEqual(long.MaxValue, long.MaxValue.CeilTo100());
      Assert.AreEqual(200L, 101L.CeilTo100());
      transfers.Add(new(-1, 0, long.MaxValue, ScoreTransferReason.Riichi));
      transfers.Add(new(-1, 0, long.MaxValue, ScoreTransferReason.Riichi));
      Assert.AreEqual(long.MaxValue, transfers.ExtraScoreChange(0));
      Assert.AreEqual(long.MinValue, transfers.DeltaScore(-1));
    }
  }
}
