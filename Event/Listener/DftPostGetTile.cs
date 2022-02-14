using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RabiRiichi.Event.Listener {
    class DftPostGetTile : ListenerBase {
        public override uint CanListen(EventBase ev) => Priority.Default;

        public override async Task<bool> Handle(EventBase ev) {
            var e = (GetTileEvent) ev;
            var player = e.player;
            player.hand.AddGroup(e.group, e.source);
            await e.game.actionManager.OnDrawTile(player.hand, null, true);
            return false;
        }
    }
}
