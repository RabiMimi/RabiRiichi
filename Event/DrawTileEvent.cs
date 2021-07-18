using RabiRiichi.Riichi;

namespace RabiRiichi.Event {
    class DrawTileEvent : EventBase {
        #region Request
        public int player;
        #endregion

        #region Response
        public Tile tile;
        #endregion
    }
}
