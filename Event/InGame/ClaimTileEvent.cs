using RabiRiichi.Action;
using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class ClaimTileEvent : BroadcastPlayerEvent {
        public override string name => "claim_tile";

        #region Request
        [RabiBroadcast] public GameTile tile;
        [RabiBroadcast] public MenLike group;
        #endregion

        #region Response
        public readonly WaitPlayerActionEvent waitEvent;
        [RabiBroadcast] public DiscardReason reason = DiscardReason.None;
        #endregion

        public ClaimTileEvent(EventBase parent, int playerId, MenLike group, GameTile tile) : base(parent, playerId) {
            this.group = group;
            this.tile = tile;
            waitEvent = new WaitPlayerActionEvent(this);
        }
    }
}
