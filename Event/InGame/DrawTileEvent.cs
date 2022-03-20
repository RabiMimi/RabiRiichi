﻿using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DrawTileEvent : BroadcastPlayerEvent {
        public override string name => "draw_tile";

        #region Request
        [RabiBroadcast] public TileSource source;
        [RabiBroadcast] public DiscardReason reason;
        #endregion

        #region Response
        public readonly WaitPlayerActionEvent waitEvent;
        #endregion

        public DrawTileEvent(EventBase parent, int playerId, TileSource source, DiscardReason reason) : base(parent, playerId) {
            this.source = source;
            this.reason = reason;
            waitEvent = new WaitPlayerActionEvent(this);
        }
    }
}
