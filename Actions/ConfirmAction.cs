using RabiRiichi.Utils;

namespace RabiRiichi.Actions {
  /// <summary>
  /// <see cref="ConfirmAction"/>的结果一定为true，
  /// 用户跳过会使用<see cref="SkipAction"/>处理。
  /// </summary>
  public abstract class ConfirmAction(int playerId) : PlayerAction<Empty>(playerId) {
  }
}