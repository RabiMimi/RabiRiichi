using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;

namespace RabiRiichi.Events.InGame {
  public class ClaimTileEvent(EventBase parent, int playerId, MenLike group, GameTile tile) : PlayerEvent(parent, playerId) {
    public override string name => "claim_tile";

    #region Request
    [RabiBroadcast] public GameTile tile = tile;
    [RabiBroadcast] public MenLike group = group;
    #endregion

    #region Response
    [RabiBroadcast] public DiscardReason reason = DiscardReason.None;

    #endregion
  }
}