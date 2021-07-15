using RabiRiichi.Riichi;
using System.Threading.Tasks;
using System.Diagnostics;

namespace RabiRiichi.Event.Listener {
    public class DefaultOnDealHand : ListenerBase {
        public override uint CanListen(EventBase ev) => Priority.Low;

        public override Task<bool> Handle(EventBase ev) {
            var e = (DealHandEvent) ev;
            var yama = ev.game.wall;
            if (yama.remaining.Count < Game.HandSize) {
                ev.Finish();
                return Task.FromResult(true);
            }
            e.hand = new Tiles(ev.game.rand.Choice(yama.remaining, Game.HandSize));
            e.hand.Sort();
            return Task.FromResult(true);
        }
    }
}
