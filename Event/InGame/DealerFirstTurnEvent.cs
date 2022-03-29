using RabiRiichi.Core;

namespace RabiRiichi.Event.InGame {
    public class DealerFirstTurnEvent : BroadcastPlayerEvent {
        public override string name => "dealer_first_turn";

        #region request
        public GameTile incoming;
        #endregion

        #region Response
        public readonly WaitPlayerActionEvent waitEvent;
        #endregion

        public DealerFirstTurnEvent(EventBase parent, int playerId, GameTile incoming) : base(parent, playerId) {
            this.incoming = incoming;
            waitEvent = new WaitPlayerActionEvent(this);
        }
    }
}