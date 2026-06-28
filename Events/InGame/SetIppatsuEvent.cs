
using RabiRiichi.Communication;

namespace RabiRiichi.Events.InGame {
  public class SetIppatsuEvent(EventBase parent, int playerId, bool ippatsu) : PlayerEvent(parent, playerId) {
    public override string name => "set_ippatsu";

    #region Request
    [RabiBroadcast] public bool ippatsu = ippatsu;

    #endregion
  }
}