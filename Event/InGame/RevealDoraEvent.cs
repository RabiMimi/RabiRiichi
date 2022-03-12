using RabiRiichi.Interact;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    /// <summary>
    /// 翻宝牌。若Player为null，说明是一局开始时翻的第一个宝牌
    /// </summary>
    public class RevealDoraEvent : BroadcastPlayerEvent {
        public override string name => "reveal_dora";

        #region Response
        [RabiBroadcast]
        public Tile dora;
        public Tile uraDora;
        #endregion

        public RevealDoraEvent(Game game, Player player = null) : base(game, player) { }
    }
}