using RabiRiichi.Riichi;
using System.Threading.Tasks;

namespace RabiRiichi.Event.Listener {
    public static class DefaultDealHand {
        public static Task PrepareHand(EventBase ev) {
            var e = (DealHandEvent) ev;
            var yama = ev.game.wall;
            if (yama.NumRemaining < Game.HandSize) {
                ev.Cancel();
                return Task.CompletedTask;
            }
            e.tiles = new Tiles(ev.game.rand.Choice(yama.remaining, Game.HandSize));
            e.tiles.Sort();
            return Task.CompletedTask;
        }

        
        public static Task DealHand(EventBase ev) {
            var e = (DealHandEvent) ev;
            var player = e.player;
            foreach (var tile in e.tiles) {
                player.hand.Add(new Riichi.GameTile {
                    tile = tile,
                    source = Riichi.TileSource.Wall,
                });
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<DealHandEvent>(PrepareHand, Priority.Prepare);
        }
    }
}
