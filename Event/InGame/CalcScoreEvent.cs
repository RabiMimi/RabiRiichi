using RabiRiichi.Communication;
using RabiRiichi.Util;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Event.InGame {
    public enum ScoreTransferReason {
        Ron,
        Tsumo,
        Ryuukyoku,
        NagashiMangan,
        Accumulated,
        Pao,
    }

    public class ScoreTransfer : IRabiMessage {
        public RabiMessageType msgType => RabiMessageType.Unnecessary;
        [RabiBroadcast] public int from;
        [RabiBroadcast] public int to;
        [RabiBroadcast] public int points;
        [RabiBroadcast] public ScoreTransferReason reason;
        public ScoreTransfer(int from, int to, int points, ScoreTransferReason reason, bool ceilTo100 = true) {
            this.from = from;
            this.to = to;
            this.reason = reason;
            if (ceilTo100) {
                this.points = points.CeilTo100();
            } else {
                this.points = points;
            }
        }
    }

    public class ScoreTransferList : List<ScoreTransfer> {
        public readonly int playerCount;

        public ScoreTransferList(int playerCount) {
            this.playerCount = playerCount;
        }

        public int DeltaScore(int playerId)
            => this.Where(x => x.to == playerId).Sum(x => x.points)
                - this.Where(x => x.from == playerId).Sum(x => x.points);

        public int ExtraScoreChange(int playerId)
            => this.Where(x => x.from < 0 && x.to == playerId).Sum(x => x.points)
                - this.Where(x => x.from == playerId && x.to < 0).Sum(x => x.points);
    }

    public class CalcScoreEvent : EventBase {
        public override string name => "calc_score";

        #region Request
        [RabiBroadcast] public readonly AgariInfoList agariInfos;
        [RabiBroadcast] public readonly bool isAgari;
        #endregion

        #region Response
        [RabiBroadcast] public readonly ScoreTransferList scoreChange;
        #endregion

        public CalcScoreEvent(EventBase parent, AgariInfoList agariInfos) : base(parent) {
            this.agariInfos = agariInfos;
            this.isAgari = true;
            scoreChange = new ScoreTransferList(game.config.playerCount);
        }
    }
}