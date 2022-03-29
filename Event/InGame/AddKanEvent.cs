using RabiRiichi.Communication;
using RabiRiichi.Core;

namespace RabiRiichi.Event.InGame {
    /// <summary>
    /// 加杠/暗杠/大明杠后的处理
    /// </summary>
    public class AddKanEvent : BroadcastPlayerEvent {
        public override string name => "add_kan";
        #region Request
        [RabiBroadcast] public Kan kan;
        [RabiBroadcast] public TileSource kanSource;
        public GameTile incoming;
        #endregion

        public AddKanEvent(KanEvent ev) : base(ev, ev.playerId) {
            kan = ev.kan;
            incoming = ev.incoming;
            kanSource = ev.kanSource;
        }
    }
}