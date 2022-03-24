using RabiRiichi.Core;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class DealHandListener {
        public static Task DealHand(DealHandEvent e) {
            var player = e.player;
            int count = Game.HAND_SIZE;
            if (player.IsDealer) {
                count++;
            }
            foreach (var tile in e.game.wall.Draw(count)) {
                player.hand.Add(tile);
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<DealHandEvent>(DealHand, EventPriority.Execute);
        }
    }
}
