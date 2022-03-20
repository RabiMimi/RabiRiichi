using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class LateClaimTileEvent : BroadcastPlayerEvent {
        public override string name => "late_claim_tile";

        #region Request
        [RabiBroadcast] public DiscardReason reason = DiscardReason.None;
        #endregion

        #region Response
        public readonly WaitPlayerActionEvent waitEvent;
        #endregion

        public LateClaimTileEvent(ClaimTileEvent parent) : base(parent, parent.playerId) {
            waitEvent = new WaitPlayerActionEvent(this);
            reason = parent.reason;
        }
    }
}
