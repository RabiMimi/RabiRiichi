using RabiRiichi.Riichi;

namespace RabiRiichi.Event {
    enum DrawTileType {
        Wall,
        OpenRinshan,
        CloseRinshan
    }
    class DrawTileEvent : EventBase {
        #region Request
        /// <summary>
        /// 是否摸岭上牌
        /// </summary>
        public DrawTileType type;
        public int player;
        #endregion

        #region Response
        public Tile tile;
        public Tile doraIndicator = Tile.Empty;
        #endregion
    }
}
