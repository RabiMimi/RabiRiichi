using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    /// <summary>
    /// 一般来讲，<see cref="ConfirmAction"/>的结果一定为true，
    /// 用户跳过会使用<see cref="SkipAction"/>处理。
    /// </summary>
    public abstract class ConfirmAction : PlayerAction<bool> {
        public ConfirmAction(Player player) : base(player) { }
    }
}