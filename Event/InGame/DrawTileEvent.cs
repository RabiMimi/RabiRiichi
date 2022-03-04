using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DrawTileEvent : EventBase {
        #region Request
        public TileSource source;
        public Player player;
        #endregion

        #region Response
        public Tile tile;
        #endregion

        public DrawTileEvent(Game game, Player player, TileSource source = TileSource.Wall, Tile tile = default) : base(game) {
            this.player = player;
            this.source = source;
            this.tile = tile;
        }
    }
}
