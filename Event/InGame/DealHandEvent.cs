using RabiRiichi.Interact;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DealHandEvent : PrivatePlayerEvent {
        public override string name => "deal_hand";

        #region Response
        // TODO: Custom JsonConverter for Tile, Tiles
        [RabiPrivate]
        public Tiles tiles;
        #endregion

        public DealHandEvent(Game game, int playerId) : base(game, playerId) { }
    }
}
