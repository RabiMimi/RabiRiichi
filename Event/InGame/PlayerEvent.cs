using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class PlayerEvent : EventBase, IWithPlayer {
        public Player player { get; protected set; }
        public PlayerEvent(Game game, Player player) : base(game) {
            this.player = player;
        }
    }
}