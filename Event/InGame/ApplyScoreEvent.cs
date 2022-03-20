using RabiRiichi.Communication;

namespace RabiRiichi.Event.InGame {
    public class ApplyScoreEvent : EventBase {
        public override string name => "apply_score";

        #region Request
        [RabiBroadcast] public readonly ScoreTransferList scoreChange;
        #endregion

        public ApplyScoreEvent(EventBase parent, ScoreTransferList scoreChange) : base(parent) {
            this.scoreChange = scoreChange;
        }
    }
}