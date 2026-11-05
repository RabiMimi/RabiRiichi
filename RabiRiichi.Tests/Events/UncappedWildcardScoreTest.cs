using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core.Config;
using RabiRiichi.Communication.Proto;
using RabiRiichi.Communication.Sync;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Events;
using RabiRiichi.Events.InGame;
using RabiRiichi.Events.InGame.Listener;
using RabiRiichi.Generated.Events.InGame;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Patterns;
using RabiRiichi.Utils;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Events {
  [TestClass]
  public class UncappedWildcardScoreTest {
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LargeUncappedPaymentsRemainExact(bool tsumo) {
      var game = WildcardFixture.Game();
      foreach (var player in game.players) player.points = 2000;
      var score = new ScoreStorage { result = new(ScoringOption.Aotenjou) { han = 200, yaku = 200, fu = 40 } };
      var incoming = WildcardFixture.Incoming("*", tsumo);
      int from = tsumo ? 0 : 1;
      var calc = new CalcScoreEvent(new InitGameEvent(game), new AgariInfoList(from, incoming, tsumo, new AgariInfo(0, score)));
      game.players[0].hand.agariTile = incoming;
      await CalcScoreListener.CalcScore(calc);
      Assert.AreEqual((BigInteger)40 << 202, score.result.BaseScore);
      var payment = (score.result.BaseScore * (tsumo ? 2 : 6)).CeilTo100();
      Assert.IsTrue(payment > BigInteger.One << 128);
      Assert.IsTrue(calc.scoreChange.Where(t => t.reason is ScoreTransferReason.Ron or ScoreTransferReason.Tsumo)
          .All(t => t.points == payment));
      var delta = calc.scoreChange.DeltaScore(0);
      await ApplyScoreListener.ApplyScore(new ApplyScoreEvent(calc, calc.scoreChange));
      Assert.AreEqual(2000 + delta, game.players[0].points);
      Assert.AreEqual(2000 - payment, game.players[1].points);
      Assert.AreEqual((BigInteger)game.players.Length * 2000, game.players.Aggregate(BigInteger.Zero, (sum, player) => sum + player.points));
      Assert.AreEqual(0, game.PlayersByRank[0].id);
    }

    [TestMethod]
    public async Task LargePaymentPreservesExactBalanceAndDecimalWireValue() {
      var game = WildcardFixture.Game();
      game.players[0].points = 2000;
      game.players[1].points = 2000;
      var amount = (BigInteger.One << 128) + 1;
      var transfer = new ScoreTransfer(1, 0, amount, ScoreTransferReason.Ron, ceilTo100: false);
      // Shared with the web client's beyond-128-bit precision regression.
      var expected = new byte[] { 0x08, 0x01, 0x1a, 0x27 }
          .Concat(Encoding.UTF8.GetBytes("340282366920938463463374607431768211457"))
          .Concat(new byte[] { 0x20, 0x01 }).ToArray();
      CollectionAssert.AreEqual(expected, transfer.ToProto().ToByteArray());
      var decoded = ScoreTransferMsg.Parser.ParseFrom(transfer.ToProto().ToByteArray());
      Assert.AreEqual(amount.ToString(), decoded.Points);
      await ApplyScoreListener.ApplyScore(new ApplyScoreEvent(new InitGameEvent(game), new ScoreTransferList { transfer }));
      Assert.AreEqual(2000 + amount, game.players[0].points);
      Assert.AreEqual(2000 - amount, game.players[1].points);
    }

    [TestMethod]
    public void HugeScoresSurviveStatePreviewAndFinalResultSerialization() {
      var game = WildcardFixture.Game();
      var amount = (BigInteger.One << 256) + 123;
      game.players[0].points = -amount;
      var state = new PlayerState(game.players[0], 0).ToProto(0);
      var decoded = global::RabiRiichi.Generated.Communication.Sync.PlayerStateMsg.Parser.ParseFrom(state.ToByteArray());
      Assert.AreEqual((-amount).ToString(), decoded.Points);
      var preview = ProtoConverters.ConvertTenpaiInfo(new TenpaiInfo { winningTile = new Tile("1m"), points = amount });
      Assert.AreEqual(amount.ToString(), preview.Points);
      var stop = new StopGameEvent(new InitGameEvent(game));
      stop.endGamePoints.AddRange(new[] { amount, -amount });
      var result = ProtoConverters.ConvertStopGameEvent(stop);
      CollectionAssert.AreEqual(new[] { amount.ToString(), (-amount).ToString() }, result.EndGamePoints.ToArray());
    }

    [TestMethod]
    public void RiichiThresholdHasNoArtificialNegativeFloorAndAddsExactly() {
      var config = new GameConfig { riichiPolicy = RiichiPolicy.None };
      Assert.IsNull(config.MinRiichiPoints);
      config.riichiPolicy = RiichiPolicy.SufficientPoints;
      config.pointThreshold.validPointsRange[0] = long.MaxValue;
      config.pointThreshold.riichiPoints = long.MaxValue;
      Assert.AreEqual((BigInteger)long.MaxValue * 2, config.MinRiichiPoints.Value);
    }

    [TestMethod]
    public void TransfersAccumulateAndRoundWithoutFixedWidthLimits() {
      var amount = BigInteger.One << 256;
      var rounded = amount.CeilTo100();
      Assert.AreEqual(BigInteger.Zero, rounded % 100);
      Assert.IsTrue(rounded >= amount && rounded < amount + 100);
      var transfers = new ScoreTransferList {
        new(1, 0, amount, ScoreTransferReason.Ron, false),
        new(2, 0, amount, ScoreTransferReason.Ron, false),
        new(0, 3, amount, ScoreTransferReason.Ron, false),
      };
      Assert.AreEqual(amount, transfers.DeltaScore(0));
      Assert.AreEqual(amount - 100, transfers.ApplyTo(0, -100));
      transfers.Add(new(-1, 0, amount, ScoreTransferReason.Riichi, false));
      transfers.Add(new(-1, 0, amount, ScoreTransferReason.Riichi, false));
      Assert.AreEqual(amount * 2, transfers.ExtraScoreChange(0));
      Assert.AreEqual(-amount * 2, transfers.DeltaScore(-1));
    }
  }
}
