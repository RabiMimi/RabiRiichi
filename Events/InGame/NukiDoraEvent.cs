using RabiRiichi.Communication;
using RabiRiichi.Core;

namespace RabiRiichi.Events.InGame {
  /// <summary>
  /// 拔北。会开启抢拔北（视同抢槓）的窗口，窗口关闭后由<see cref="AddNukiDoraEvent"/>处理。
  /// </summary>
  public class NukiDoraEvent : PlayerEvent {
    public override string name => "nuki_dora";
    #region Request
    /// <summary> 被拔出的北 </summary>
    [RabiBroadcast] public readonly GameTile incoming;
    #endregion

    #region Response
    public readonly WaitPlayerActionEvent waitEvent;
    #endregion

    public NukiDoraEvent(EventBase parent, int playerId, GameTile incoming) : base(parent, playerId) {
      this.incoming = incoming;
      waitEvent = new WaitPlayerActionEvent(this);
    }
  }
}
