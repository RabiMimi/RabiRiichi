using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;

namespace RabiRiichi.Events.InGame {
    public class DrawTileEvent : BroadcastPlayerEvent {
        public override string name => "draw_tile";

        #region Request
        [RabiBroadcast] public TileSource source;
        #endregion

        #region Response
        [RabiPrivate] public GameTile tile;
        public readonly WaitPlayerActionEvent waitEvent;
        #endregion

        public DrawTileEvent(EventBase parent, int playerId, TileSource source) : base(parent, playerId) {
            this.source = source;
            waitEvent = new WaitPlayerActionEvent(this);
        }
    }
}
