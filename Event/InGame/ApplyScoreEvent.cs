using RabiRiichi.Interact;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class ApplyScoreEvent : EventBase {
        public override string name => "apply_score";

        #region Request
        [RabiBroadcast] public readonly ScoreTransferList scoreChange;
        #endregion

        public ApplyScoreEvent(Game game, ScoreTransferList scoreChange) : base(game) {
            this.scoreChange = scoreChange;
        }
    }
}