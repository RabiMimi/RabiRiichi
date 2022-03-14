using RabiRiichi.Interact;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DiscardTileEvent : BroadcastPlayerEvent {
        public override string name => "discard_tile";

        #region Request
        [RabiBroadcast] public GameTile tile;
        #endregion

        #region Response
        #endregion

        public DiscardTileEvent(Game game, int playerId, GameTile tile) : base(game, playerId) {
            this.tile = tile;
        }
    }
}
