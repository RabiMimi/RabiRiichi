using RabiRiichi.Riichi;

namespace RabiRiichi.Event {
    class GetTileEvent : EventBase {
        #region Request
        public TileSource source;
        public int player;
        public GameTiles group;
        #endregion

        #region Response
        #endregion
    }
}
