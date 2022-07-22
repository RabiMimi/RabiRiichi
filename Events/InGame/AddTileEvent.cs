using RabiRiichi.Communication;
using RabiRiichi.Core;

namespace RabiRiichi.Events.InGame {
    /// <summary>
    /// 抽牌以后的处理（用户做出回应以后）
    /// </summary>
    public class AddTileEvent : PlayerEvent {
        public override string name => "add_tile";
        #region Request
        [RabiPrivate] public GameTile incoming;
        #endregion

        public AddTileEvent(DrawTileEvent ev) : base(ev, ev.playerId) {
            incoming = ev.tile;
        }

        public AddTileEvent(PlayerEvent ev, GameTile incoming) : base(ev, ev.playerId) {
            this.incoming = incoming;
        }
    }
}