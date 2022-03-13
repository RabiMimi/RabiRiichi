using RabiRiichi.Interact;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public abstract class PlayerEvent : EventBase, IRabiPlayerMessage {
        public Player player => game.GetPlayer(playerId);
        [RabiBroadcast] public int playerId { get; init; }

        public PlayerEvent(Game game, int playerId) : base(game) {
            this.playerId = playerId;
        }
    }

    [RabiPrivate]
    public abstract class PrivatePlayerEvent : PlayerEvent {
        public PrivatePlayerEvent(Game game, int playerId) : base(game, playerId) { }
    }

    [RabiBroadcast]
    public abstract class BroadcastPlayerEvent : PlayerEvent {
        public BroadcastPlayerEvent(Game game, int playerId) : base(game, playerId) { }
    }

    [RabiPrivate]
    public abstract class IgnoredEvent : PlayerEvent {
        public IgnoredEvent(Game game) : base(game, -1) { }
    }
}