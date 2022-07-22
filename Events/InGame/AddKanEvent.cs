using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;

namespace RabiRiichi.Events.InGame {
    /// <summary>
    /// 加杠/暗杠/大明杠后的处理
    /// </summary>
    public class AddKanEvent : PlayerEvent {
        public override string name => "add_kan";
        #region Request
        [RabiBroadcast] public Kan kan;
        [RabiBroadcast] public GameTile incoming;
        [RabiBroadcast] public TileSource kanSource;
        #endregion

        public AddKanEvent(KanEvent ev) : base(ev, ev.playerId) {
            kan = ev.kan;
            incoming = ev.incoming;
            kanSource = ev.kanSource;
        }
    }
}