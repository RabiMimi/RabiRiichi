using RabiRiichi.Action;
using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DiscardTileEvent : BroadcastPlayerEvent {
        public override string name => "discard_tile";

        #region Request
        [RabiBroadcast] public GameTile tile;
        [RabiBroadcast] public DiscardReason reason;
        #endregion

        #region Response
        public readonly WaitPlayerActionEvent waitEvent;
        #endregion

        public DiscardTileEvent(Game game, int playerId, GameTile tile, DiscardReason reason) : base(game, playerId) {
            this.tile = tile;
            this.reason = reason;
            waitEvent = new WaitPlayerActionEvent(game);
        }
    }

    public class RiichiEvent : DiscardTileEvent {
        public override string name => "riichi";

        public RiichiEvent(Game game, int playerId, GameTile tile, DiscardReason reason) : base(game, playerId, tile, reason) { }
    }
}
