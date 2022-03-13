using RabiRiichi.Interact;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DrawTileEvent : BroadcastPlayerEvent {
        public override string name => "draw_tile";

        #region Request
        [RabiBroadcast]
        public TileSource source;
        #endregion

        #region Response
        [RabiPrivate]
        public Tile tile;
        #endregion

        public DrawTileEvent(Game game, int playerId, TileSource source = TileSource.Wall, Tile tile = default) : base(game, playerId) {
            this.source = source;
            this.tile = tile;
        }
    }
}
