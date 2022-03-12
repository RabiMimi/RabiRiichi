using RabiRiichi.Interact;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public abstract class PlayerEvent : EventBase, IRabiPlayerMessage {
        public Player player { get; protected set; }
        [RabiBroadcast] public int playerId => player.id;

        public PlayerEvent(Game game, Player player) : base(game) {
            this.player = player;
        }
    }

    [RabiPrivate]
    public abstract class PrivatePlayerEvent : PlayerEvent {
        public PrivatePlayerEvent(Game game, Player player) : base(game, player) { }
    }

    [RabiBroadcast]
    public abstract class BroadcastPlayerEvent : PlayerEvent {
        public BroadcastPlayerEvent(Game game, Player player) : base(game, player) { }
    }
}