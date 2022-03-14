using RabiRiichi.Interact;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Event.InGame {
    public class ScoreTransfer {
        public readonly int from;
        public readonly int to;
        public readonly int points;
        public ScoreTransfer(int from, int to, int points) {
            this.from = from;
            this.to = to;
            this.points = points;
        }
    }

    public class ScoreTransferList : List<ScoreTransfer> {
        public int DeltaScore(int playerId)
            => this.Where(x => x.to == playerId).Sum(x => x.points)
                - this.Where(x => x.from == playerId).Sum(x => x.points);
    }

    public class CalcScoreEvent : EventBase {
        public override string name => "calc_score";

        #region Request
        [RabiBroadcast] public readonly AgariInfoList agariInfos;
        [RabiBroadcast] public readonly bool isAgari;
        #endregion

        #region Response
        [RabiBroadcast] public readonly ScoreTransferList scoreChange = new();
        #endregion

        public CalcScoreEvent(Game game, AgariInfoList agariInfos) : base(game) {
            this.agariInfos = agariInfos;
            this.isAgari = true;
        }

        // TODO: Another constructor for Ryuukyoku
    }
}