using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;

namespace RabiRiichi.Events.InGame {
    /// <summary>
    /// 加杠/暗杠/大明杠
    /// </summary>
    public class KanEvent : PlayerEvent {
        public override string name => "kan";
        #region Request
        [RabiBroadcast] public Kan kan;
        [RabiBroadcast] public TileSource kanSource;
        /// <summary>
        /// 杠里的牌，对于暗杠来说不一定是刚摸到的
        /// </summary>
        public readonly GameTile incoming;
        #endregion

        #region Response
        public readonly WaitPlayerActionEvent waitEvent;
        #endregion

        public KanEvent(EventBase parent, int playerId, Kan kan, GameTile incoming) : base(parent, playerId) {
            this.kan = kan;
            this.incoming = incoming;
            this.kanSource = kan.KanSource;
            this.waitEvent = new WaitPlayerActionEvent(this);
        }
    }
}