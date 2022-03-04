using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DealHandEvent : EventBase {
        #region Request
        public Player player;
        #endregion

        #region Response
        public Tiles tiles;
        #endregion

        public DealHandEvent(Game game, Player player) : base(game) {
            this.player = player;
        }
    }
}
