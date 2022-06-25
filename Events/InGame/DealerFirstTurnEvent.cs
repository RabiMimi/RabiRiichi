using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Generated.Events.InGame;

namespace RabiRiichi.Events.InGame {
    public class DealerFirstTurnEvent : BroadcastPlayerEvent {
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

        public DealerFirstTurnEventMsg ToProto(int playerId) {
            var ret = new DealerFirstTurnEventMsg {
                PlayerId = this.playerId,
            };
            if (this.playerId == playerId) {
                ret.Incoming = incoming.ToProto();
            }
            return ret;
        }
    }
}