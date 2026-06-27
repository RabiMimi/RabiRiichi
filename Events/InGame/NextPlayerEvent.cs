using RabiRiichi.Communication;

namespace RabiRiichi.Events.InGame {
  public class NextPlayerEvent(EventBase parent, int playerId) : PlayerEvent(parent, playerId) {
    public override string name => "next_player";

    #region Response
    [RabiBroadcast] public int nextPlayerId;

    #endregion
  }
}