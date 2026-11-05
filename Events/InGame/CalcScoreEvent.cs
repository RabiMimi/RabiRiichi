using RabiRiichi.Communication;
using RabiRiichi.Generated.Events.InGame;
using RabiRiichi.Utils;
using System.Collections.Generic;
using System.Numerics;

namespace RabiRiichi.Events.InGame {
  [RabiMessage]
  public class ScoreTransfer {
    [RabiBroadcast] public int from;
    [RabiBroadcast] public int to;
    [RabiBroadcast] public BigInteger points;
    [RabiBroadcast] public ScoreTransferReason reason;
    public ScoreTransfer(int from, int to, BigInteger points, ScoreTransferReason reason, bool ceilTo100 = true) {
      this.from = from;
      this.to = to;
      this.reason = reason;
      this.points = ceilTo100 ? points.CeilTo100() : points;
    }

    public ScoreTransferMsg ToProto() {
      return new ScoreTransferMsg {
        From = from,
        To = to,
        Points = points.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Reason = reason,
      };
    }
  }

  public class ScoreTransferList : List<ScoreTransfer> {
    private BigInteger NetChange(int playerId, bool extraOnly = false) {
      BigInteger result = 0;
      foreach (var transfer in this) {
        if (transfer.to == playerId && (!extraOnly || transfer.from < 0)) result += transfer.points;
        if (transfer.from == playerId && (!extraOnly || transfer.to < 0)) result -= transfer.points;
      }
      return result;
    }

    public BigInteger DeltaScore(int playerId) {
      return NetChange(playerId);
    }

    public BigInteger ExtraScoreChange(int playerId) {
      return NetChange(playerId, true);
    }

    internal BigInteger ApplyTo(int playerId, BigInteger balance) => balance + NetChange(playerId);
  }

  public class CalcScoreEvent(EventBase parent, AgariInfoList agariInfos) : EventBase(parent) {
    public override string name => "calc_score";

    #region Request
    public readonly AgariInfoList agariInfos = agariInfos;
    #endregion

    #region Response
    public readonly ScoreTransferList scoreChange = [];

    #endregion
  }
}
