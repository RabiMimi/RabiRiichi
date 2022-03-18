using RabiRiichi.Action;
using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DrawTileEvent : BroadcastPlayerEvent {
        public override string name => "draw_tile";

        #region Request
        [RabiBroadcast] public TileSource source;
        [RabiBroadcast] public DiscardReason reason;
        #endregion

        #region Response
        public readonly MultiPlayerInquiry inquiry;
        #endregion

        public DrawTileEvent(Game game, int playerId, TileSource source, DiscardReason reason) : base(game, playerId) {
            this.source = source;
            this.reason = reason;
            inquiry = new MultiPlayerInquiry(game.info);
        }
    }
}
