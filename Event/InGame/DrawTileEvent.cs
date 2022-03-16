using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DrawTileEvent : BroadcastPlayerEvent {
        public override string name => "draw_tile";

        #region Request
        [RabiBroadcast] public TileSource source;
        #endregion

        public DrawTileEvent(Game game, int playerId, TileSource source) : base(game, playerId) {
            this.source = source;
        }
    }
}
