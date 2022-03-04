using RabiRiichi.Riichi;

namespace RabiRiichi.Event {
    public enum DrawTileType {
        /// <summary> 牌山 </summary>
        Wall,
        /// <summary> 明杠 </summary> 
        OpenRinshan,
        /// <summary> 暗杠 </summary>
        CloseRinshan
    }
    public class DrawTileEvent : EventBase {
        #region Request
        /// <summary>
        /// 是否摸岭上牌
        /// </summary>
        public DrawTileType type;
        public Player player;
        #endregion

        #region Response
        public Tile tile;
        public Tile doraIndicator = Tile.Empty;
        #endregion

        public DrawTileEvent(Game game, Player player, DrawTileType type) : base(game) {
            this.player = player;
            this.type = type;
        }
    }
}
