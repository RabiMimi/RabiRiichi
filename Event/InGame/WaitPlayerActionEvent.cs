using RabiRiichi.Action;
using RabiRiichi.Communication;
using System.Collections.Generic;


namespace RabiRiichi.Event.InGame {
    [RabiIgnore]
    public class WaitPlayerActionEvent : EventBase {
        public override string name => "wait_player_action";

        #region Request
        public readonly MultiPlayerInquiry inquiry;
        public readonly MultiEventBuilder eventBuilder = new();
        #endregion

        #region Response
        public readonly List<EventBase> responseEvents = new();
        #endregion

        public WaitPlayerActionEvent(EventBase parent) : base(parent) {
            this.inquiry = new MultiPlayerInquiry(game);
        }
    }
}