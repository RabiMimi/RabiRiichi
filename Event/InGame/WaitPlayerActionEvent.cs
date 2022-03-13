using RabiRiichi.Action;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class WaitPlayerActionEvent : EventBase {
        public override string name => "wait_player_action";

        #region Request
        public MultiPlayerInquiry inquiry;
        #endregion

        #region Response
        #endregion

        public WaitPlayerActionEvent(Game game, MultiPlayerInquiry inquiry) : base(game) {
            this.inquiry = inquiry;
        }
    }
}