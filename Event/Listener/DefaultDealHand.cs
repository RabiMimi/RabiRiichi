using RabiRiichi.Riichi;
using System.Threading.Tasks;

namespace RabiRiichi.Event.Listener {
    public static class DefaultDealHand {
        public static Task PrepareHand(DealHandEvent e) {
            var yama = e.game.wall;
            if (yama.NumRemaining < Game.HandSize) {
                e.Cancel();
                return Task.CompletedTask;
            }
            e.tiles = new Tiles(e.game.rand.Choice(yama.remaining, Game.HandSize));
            e.tiles.Sort();
            return Task.CompletedTask;
        }


        public static Task DealHand(DealHandEvent e) {
            var player = e.player;
            foreach (var tile in e.tiles) {
                player.hand.Add(new GameTile(tile) {
                    source = TileSource.Wall,
                });
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<DealHandEvent>(PrepareHand, EventPriority.Prepare);
        }
    }
}
