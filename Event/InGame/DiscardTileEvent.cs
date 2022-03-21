using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DiscardTileEvent : BroadcastPlayerEvent {
        public override string name => "discard_tile";

        #region Request
        [RabiBroadcast] public GameTile tile;
        [RabiBroadcast] public DiscardReason reason;
        [RabiBroadcast] public TileSource origin;
        #endregion

        #region Response
        public readonly WaitPlayerActionEvent waitEvent;
        #endregion

        public DiscardTileEvent(EventBase parent, int playerId, GameTile tile, DiscardReason reason) : base(parent, playerId) {
            this.tile = tile;
            this.reason = reason;
            origin = tile.source;
            waitEvent = new WaitPlayerActionEvent(this);
        }
    }

    public class RiichiEvent : DiscardTileEvent {
        public override string name => "riichi";

        public RiichiEvent(EventBase parent, int playerId, GameTile tile, DiscardReason reason) : base(parent, playerId, tile, reason) { }
    }
}
