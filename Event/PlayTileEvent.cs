using RabiRiichi.Riichi;

namespace RabiRiichi.Event {
    class PlayTileEvent : EventBase {
        #region Request
        public int player;
        public GameTile tile;
        public bool riichi;
        #endregion

        #region Response
        public bool waitAction = false;
        #endregion
    }
}
