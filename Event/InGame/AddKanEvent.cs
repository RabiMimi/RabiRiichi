using RabiRiichi.Action;
using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    /// <summary>
    /// 加杠/暗杠/大明杠后的处理
    /// </summary>
    public class AddKanEvent : PlayerEvent {
        public override string name => "add_kan";
        #region Request
        [RabiBroadcast] public readonly Kan kan;
        [RabiBroadcast] public readonly TileSource kanSource;
        public readonly GameTile incoming;
        #endregion

        public AddKanEvent(Game game, int playerId, Kan kan, GameTile incoming) : base(game, playerId) {
            this.kan = kan;
            this.incoming = incoming;
            this.kanSource = kan.KanSource;
        }

        public AddKanEvent(KanEvent ev) : base(ev.game, ev.playerId) {
            kan = ev.kan;
            incoming = ev.incoming;
            kanSource = ev.kanSource;
        }
    }
}