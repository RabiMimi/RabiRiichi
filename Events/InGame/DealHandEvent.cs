using RabiRiichi.Communication;
using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Events.InGame {
  public class DealHandEvent(EventBase parent, int playerId, int count) : PlayerEvent(parent, playerId) {
    public override string name => "deal_hand";
    #region request
    [RabiBroadcast] public readonly int count = count;
    #endregion

    #region  response
    [RabiPrivate] public readonly List<GameTile> tiles = [];

    #endregion
  }
}