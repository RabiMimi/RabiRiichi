using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;

namespace RabiRiichi.Events.InGame {
    public class ClaimTileEvent : PlayerEvent {
        public override string name => "claim_tile";

        #region Request
        [RabiBroadcast] public GameTile tile;
        [RabiBroadcast] public MenLike group;
        #endregion

        #region Response
        [RabiBroadcast] public DiscardReason reason = DiscardReason.None;
        #endregion

        public ClaimTileEvent(EventBase parent, int playerId, MenLike group, GameTile tile) : base(parent, playerId) {
            this.group = group;
            this.tile = tile;
        }
    }
}
