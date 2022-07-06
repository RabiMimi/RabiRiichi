using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Events.InGame;

namespace RabiRiichi.Events.InGame {
    public class LateClaimTileEvent : BroadcastPlayerEvent {
        public override string name => "late_claim_tile";

        #region Request
        [RabiBroadcast] public DiscardReason reason = DiscardReason.None;
        #endregion

        #region Response
        public readonly Tiles forbidden = new();
        public readonly WaitPlayerActionEvent waitEvent;
        #endregion

        public LateClaimTileEvent(ClaimTileEvent parent) : base(parent, parent.playerId) {
            waitEvent = new WaitPlayerActionEvent(this);
            reason = parent.reason;
        }
    }
}
