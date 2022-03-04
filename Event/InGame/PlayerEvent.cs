using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class PlayerEvent : EventBase {
        public Player player;
        public PlayerEvent(Game game, Player player) : base(game) {
            this.player = player;
        }
    }
}