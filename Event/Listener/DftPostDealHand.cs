using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RabiRiichi.Event.Listener {
    class DftPostDealHand : ListenerBase {
        public override uint CanListen(EventBase ev) => Priority.Default;

        public override Task<bool> Handle(EventBase ev) {
            var e = (DealHandEvent) ev;
            Debug.Assert(e.game.wall.Draw(e.tiles));
            var player = e.game.GetPlayer(e.player);
            foreach (var tile in e.tiles) {
                player.hand.Add(new Riichi.GameTile {
                    tile = tile,
                    source = Riichi.TileSource.Wall,
                });
            }
            return Task.FromResult(false);
        }
    }
}
