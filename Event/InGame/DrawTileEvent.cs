using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DrawTileEvent : PlayerEvent {
        #region Request
        public TileSource source;
        #endregion

        #region Response
        public Tile tile;
        #endregion

        public DrawTileEvent(Game game, Player player, TileSource source = TileSource.Wall, Tile tile = default) : base(game, player) {
            this.source = source;
            this.tile = tile;
        }
    }
}
