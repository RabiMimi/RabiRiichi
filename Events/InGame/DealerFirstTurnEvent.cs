using RabiRiichi.Communication;
using RabiRiichi.Core;

namespace RabiRiichi.Events.InGame {
    public class DealerFirstTurnEvent : PlayerEvent {
        public override string name => "dealer_first_turn";

        #region request
        [RabiPrivate] public GameTile incoming;
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