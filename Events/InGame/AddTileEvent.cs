namespace RabiRiichi.Events.InGame {
  /// <summary>
  /// 抽牌以后的处理（用户做出回应以后）
  /// </summary>
  public class AddTileEvent(PlayerEvent ev) : PlayerEvent(ev, ev.playerId) {
    public override string name => "add_tile";
  }
}