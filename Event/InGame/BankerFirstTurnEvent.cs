namespace RabiRiichi.Event.InGame {
    public class DealerFirstTurnEvent : BroadcastPlayerEvent {
        public override string name => "dealer_first_turn";

        #region Response
        public readonly WaitPlayerActionEvent waitEvent;
        #endregion

        public DealerFirstTurnEvent(EventBase parent, int playerId) : base(parent, playerId) {
            waitEvent = new WaitPlayerActionEvent(this);
        }
    }
}