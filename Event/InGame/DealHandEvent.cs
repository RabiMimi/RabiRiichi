using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DealHandEvent : PlayerEvent {

        #region Response
        public Tiles tiles;
        #endregion

        public DealHandEvent(Game game, Player player) : base(game, player) { }
    }
}
