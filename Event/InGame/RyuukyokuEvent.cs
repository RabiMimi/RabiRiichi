using RabiRiichi.Communication;
using RabiRiichi.Riichi;

// TODO: 添加流局原因
namespace RabiRiichi.Event.InGame {
    /// <summary>
    /// 所有流局事件继承该类
    /// </summary>
    public class RyuukyokuEvent : EventBase {
        public override string name => "ryuukyoku";
        public RyuukyokuEvent(Game game) : base(game) { }
    }
}