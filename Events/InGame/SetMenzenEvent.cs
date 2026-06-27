using RabiRiichi.Communication;

namespace RabiRiichi.Events.InGame {
  public class SetMenzenEvent(EventBase parent, int playerId, bool menzen) : PlayerEvent(parent, playerId) {
    public override string name => "set_menzen";

    #region Request
    [RabiBroadcast] public bool menzen = menzen;

    #endregion
  }
}