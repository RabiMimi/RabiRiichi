using RabiRiichi.Resolver;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RabiRiichi.Event.Listener {
    class DftFinPlayTile : ListenerBase {
        public override uint CanListen(EventBase ev) => Priority.Default;

        public override Task<bool> Handle(EventBase ev) {
            var e = (PlayTileEvent)ev;
            if (!e.waitAction) {
                e.game.OnUpdate();
            }
            return Task.FromResult(false);
        }
    }
}
