using RabiRiichi.Communication;
using RabiRiichi.Generated.Events.InGame;
using RabiRiichi.Utils;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Events.InGame {
    [RabiMessage]
    public class ScoreTransfer {
        [RabiBroadcast] public int from;
        [RabiBroadcast] public int to;
        [RabiBroadcast] public long points;
        [RabiBroadcast] public ScoreTransferReason reason;
        public ScoreTransfer(int from, int to, long points, ScoreTransferReason reason, bool ceilTo100 = true) {
            this.from = from;
            this.to = to;
            this.reason = reason;
            if (ceilTo100) {
                this.points = points.CeilTo100();
            } else {
                this.points = points;
            }
        }

        public ScoreTransferMsg ToProto() {
            return new ScoreTransferMsg {
                From = from,
                To = to,
                Points = points,
                Reason = reason,
            };
        }
    }

    public class ScoreTransferList : List<ScoreTransfer> {
        public long DeltaScore(int playerId)
            => this.Where(x => x.to == playerId).Sum(x => x.points)
                - this.Where(x => x.from == playerId).Sum(x => x.points);

        public long ExtraScoreChange(int playerId)
            => this.Where(x => x.from < 0 && x.to == playerId).Sum(x => x.points)
                - this.Where(x => x.from == playerId && x.to < 0).Sum(x => x.points);
    }

    public class CalcScoreEvent : EventBase {
        public override string name => "calc_score";

        #region Request
        public readonly AgariInfoList agariInfos;
        #endregion

        #region Response
        public readonly ScoreTransferList scoreChange;
        #endregion

        public CalcScoreEvent(EventBase parent, AgariInfoList agariInfos) : base(parent) {
            this.agariInfos = agariInfos;
            scoreChange = new ScoreTransferList();
        }
    }
}