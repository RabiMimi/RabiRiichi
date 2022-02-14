using RabiRiichi.Riichi;

namespace RabiRiichi.Event {
    class GetTileEvent : EventBase {
        #region Request
        public TileSource source;
        public Player player;
        public GameTile incoming;
        public GameTiles group;
        #endregion

        #region Response
        #endregion
    }
}
