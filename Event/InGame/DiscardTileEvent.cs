using RabiRiichi.Communication;
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

    public class RiichiEvent : DiscardTileEvent {
        public override string name => "riichi";

        public RiichiEvent(Game game, int playerId, GameTile tile) : base(game, playerId, tile) { }
    }
}
