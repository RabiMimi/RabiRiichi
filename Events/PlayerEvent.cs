using RabiRiichi.Communication;
using RabiRiichi.Core;

namespace RabiRiichi.Events {
  public abstract class PlayerEvent(EventBase parent, int playerId) : EventBase(parent), IRabiPlayerMessage {
    public Player player => game.GetPlayer(playerId);
    [RabiBroadcast] public int playerId { get; init; } = playerId;
  }

  [RabiPrivate]
  public abstract class PrivatePlayerEvent(EventBase parent, int playerId) : PlayerEvent(parent, playerId) {
  }
}