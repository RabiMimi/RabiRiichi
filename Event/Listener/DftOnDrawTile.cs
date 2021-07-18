using RabiRiichi.Riichi;
using System.Threading.Tasks;

namespace RabiRiichi.Event.Listener {
    public class DftOnDrawTile : ListenerBase {
        public override uint CanListen(EventBase ev) => Priority.Default;

        public override Task<bool> Handle(EventBase ev) {
            var e = (DrawTileEvent) ev;
            int drawCount = e.type == DrawTileType.Wall ? 1 : 2;
            var yama = ev.game.wall;
            if (yama.NumRemaining < drawCount) {
                ev.Finish();
                return Task.FromResult(true);
            }
            var tiles  = ev.game.rand.Choice(yama.remaining, drawCount);
            e.tile = tiles[0];
            if (drawCount > 1) {
                e.doraIndicator = tiles[1];
            }
            return Task.FromResult(true);
        }
    }
}
