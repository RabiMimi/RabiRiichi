using RabiRiichi.Communication;

namespace RabiRiichi.Events.InGame {
  public class IncreaseJunEvent(EventBase parent, int playerId) : PlayerEvent(parent, playerId) {
    public override string name => "increase_jun";

    #region Response
    [RabiBroadcast] public int increasedJun;

    #endregion
  }
}