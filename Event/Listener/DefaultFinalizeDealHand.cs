using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RabiRiichi.Event.Listener {
    class DefaultFinalizeDealHand : ListenerBase {
        public override uint CanListen(EventBase ev) => Priority.Low;

        public override Task<bool> Handle(EventBase ev) {
            var e = (DealHandEvent) ev;
            Debug.Assert(e.game.wall.Draw(e.hand));
            return Task.FromResult(false);
        }
    }
}
