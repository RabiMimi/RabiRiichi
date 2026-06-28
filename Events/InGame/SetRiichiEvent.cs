using RabiRiichi.Communication;
using RabiRiichi.Core;

namespace RabiRiichi.Events.InGame {
  public class SetRiichiEvent(EventBase parent, int playerId, GameTile riichiTile, bool wRiichi) : PlayerEvent(parent, playerId) {
    public override string name => "set_riichi";

    #region Request
    [RabiBroadcast] public GameTile riichiTile = riichiTile;
    [RabiBroadcast] public bool wRiichi = wRiichi;

    #endregion
  }
}