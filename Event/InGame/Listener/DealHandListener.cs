using RabiRiichi.Riichi;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class DealHandListener {
        public static Task DealHand(DealHandEvent e) {
            var player = e.player;
            int count = Game.HandSize;
            if (player.IsBanker) {
                count++;
            }
            foreach (var tile in e.game.wall.Draw(count)) {
                player.hand.Add(tile);
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<DealHandEvent>(DealHand, EventPriority.Execute);
        }
    }
}
