using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;

namespace RabiRiichi.Events.InGame {
  /// <summary>
  /// 加杠/暗杠/大明杠后的处理
  /// </summary>
  public class AddKanEvent(KanEvent ev) : PlayerEvent(ev, ev.playerId) {
    public override string name => "add_kan";
    #region Request
    [RabiBroadcast] public Kan kan = ev.kan;
    [RabiBroadcast] public GameTile incoming = ev.incoming;
    [RabiBroadcast] public TileSource kanSource = ev.kanSource;

    #endregion
  }
}