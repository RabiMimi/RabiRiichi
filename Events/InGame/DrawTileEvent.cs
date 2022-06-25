using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Events.InGame;

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

        public DrawTileEventMsg ToProto(int playerId) {
            var ret = new DrawTileEventMsg {
                PlayerId = this.playerId,
                Source = source,
            };
            if (this.playerId == playerId) {
                ret.Tile = tile?.ToProto();
            }
            return ret;
        }
    }
}
