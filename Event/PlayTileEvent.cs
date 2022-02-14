using RabiRiichi.Riichi;

namespace RabiRiichi.Event {
    class PlayTileEvent : EventBase {
        #region Request
        public Player player;
        public GameTile tile;
        public bool riichi;
        #endregion

        #region Response
        public bool waitAction = false;
        #endregion
    }
}
