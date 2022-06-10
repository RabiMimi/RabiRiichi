using RabiRiichi.Communication;
using RabiRiichi.Core;

namespace RabiRiichi.Events {
    public abstract class PlayerEvent : EventBase, IRabiPlayerMessage {
        public Player player => game.GetPlayer(playerId);
        [RabiBroadcast] public int playerId { get; init; }

        public PlayerEvent(EventBase parent, int playerId) : base(parent) {
            this.playerId = playerId;
        }
    }

    [RabiPrivate]
    public abstract class PrivatePlayerEvent : PlayerEvent {
        public PrivatePlayerEvent(EventBase parent, int playerId) : base(parent, playerId) { }
    }

    [RabiBroadcast]
    public abstract class BroadcastPlayerEvent : PlayerEvent {
        public BroadcastPlayerEvent(EventBase parent, int playerId) : base(parent, playerId) { }
    }
}