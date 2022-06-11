using RabiRiichi.Communication;
using RabiRiichi.Core;

namespace RabiRiichi.Events.InGame {
    public class DiscardTileEvent : BroadcastPlayerEvent {
        public override string name => "discard_tile";

        #region Request
        [RabiPrivate] public GameTile incoming;
        [RabiBroadcast] public GameTile tile;
        [RabiBroadcast] public DiscardReason reason;
        /// <summary> 用于判定摸切还是手切 </summary>
        [RabiBroadcast] public TileSource origin;
        #endregion

        #region Response
        public readonly WaitPlayerActionEvent waitEvent;
        #endregion

        public DiscardTileEvent(EventBase parent, int playerId, GameTile tile, GameTile incoming, DiscardReason reason) : base(parent, playerId) {
            this.tile = tile;
            this.incoming = incoming;
            this.reason = reason;
            // 此时tile还没有被加入手牌，保存当前source以判定摸切还是手切
            origin = tile.source;
            waitEvent = new WaitPlayerActionEvent(this);
        }
    }

    public class RiichiEvent : DiscardTileEvent {
        public override string name => "riichi";

        public RiichiEvent(EventBase parent, int playerId, GameTile tile, GameTile incoming, DiscardReason reason) : base(parent, playerId, tile, incoming, reason) { }
    }
}
