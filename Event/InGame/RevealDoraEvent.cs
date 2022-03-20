using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    /// <summary>
    /// 翻宝牌。若Player为null，说明是一局开始时翻的第一个宝牌
    /// </summary>
    public class RevealDoraEvent : BroadcastPlayerEvent {
        public override string name => "reveal_dora";

        #region Response
        [RabiBroadcast] public Tile dora;
        #endregion

        public RevealDoraEvent(EventBase parent, int playerId = -1) : base(parent, playerId) { }
    }
}