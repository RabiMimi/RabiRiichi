using RabiRiichi.Communication;
using RabiRiichi.Core;

namespace RabiRiichi.Events.InGame {
  /// <summary>
  /// 抢拔北窗口关闭后处理拔北：将北放到拔北宝牌区，并从岭上补一张牌。
  /// </summary>
  public class AddNukiDoraEvent(NukiDoraEvent ev) : PlayerEvent(ev, ev.playerId) {
    public override string name => "add_nuki_dora";
    #region Request
    [RabiBroadcast] public readonly GameTile incoming = ev.incoming;
    #endregion
  }
}
