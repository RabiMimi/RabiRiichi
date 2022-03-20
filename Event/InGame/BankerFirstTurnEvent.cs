namespace RabiRiichi.Event.InGame {
    public class BankerFirstTurnEvent : BroadcastPlayerEvent {
        public override string name => "banker_first_turn";

        #region Response
        public readonly WaitPlayerActionEvent waitEvent;
        #endregion

        public BankerFirstTurnEvent(EventBase parent, int playerId) : base(parent, playerId) {
            waitEvent = new WaitPlayerActionEvent(this);
        }
    }
}