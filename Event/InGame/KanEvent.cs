using RabiRiichi.Communication;
using RabiRiichi.Core;

namespace RabiRiichi.Event.InGame {
    /// <summary>
    /// 加杠/暗杠/大明杠
    /// </summary>
    public class KanEvent : BroadcastPlayerEvent {
        public override string name => "kan";
        #region Request
        [RabiBroadcast] public readonly Kan kan;
        [RabiBroadcast] public readonly TileSource kanSource;
        public readonly GameTile incoming;
        #endregion

        #region Response
        [RabiBroadcast] public readonly WaitPlayerActionEvent waitEvent;
        #endregion

        public KanEvent(EventBase parent, int playerId, Kan kan, GameTile incoming) : base(parent, playerId) {
            this.kan = kan;
            this.incoming = incoming;
            this.kanSource = kan.KanSource;
            this.waitEvent = new WaitPlayerActionEvent(this);
        }
    }
}