using RabiRiichi.Interact;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DrawTileEvent : PrivatePlayerEvent {
        #region Request
        [RabiPrivate]
        public TileSource source;
        #endregion

        #region Response
        [RabiPrivate]
        public Tile tile;
        #endregion

        public DrawTileEvent(Game game, Player player, TileSource source = TileSource.Wall, Tile tile = default) : base(game, player) {
            this.source = source;
            this.tile = tile;
        }
    }
}
