using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RabiRiichi.Event.Listener {
    class DftPostPlayTile : ListenerBase {
        public override uint CanListen(EventBase ev) => Priority.Default;

        public override async Task<bool> Handle(EventBase ev) {
            var e = (PlayTileEvent) ev;
            var player = e.player;
            player.hand.Play(e.tile);
            if (await e.game.actionManager.OnDiscardTile(player.hand, e.tile)) {
                e.waitAction = true;
            } else {
                e.game.eventBus.Queue(new DrawTileEvent {
                    game = e.game,
                    type = DrawTileType.Wall,
                    player = player.NextPlayer,
                });
            }
            return false;
        }
    }
}
