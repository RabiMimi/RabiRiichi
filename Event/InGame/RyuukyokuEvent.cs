using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    /// <summary>
    /// 所有流局事件继承该类
    /// </summary>
    public abstract class RyuukyokuEvent : PlayerEvent {
        public RyuukyokuEvent(Game game, int playerId) : base(game, playerId) { }
    }
}