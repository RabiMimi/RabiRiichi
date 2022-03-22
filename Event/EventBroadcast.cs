using RabiRiichi.Communication;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Event {
    public static class EventBroadcast {
        public static Task Send(EventBase ev) {
            var players = ev.game.players.AsEnumerable();
            ev.BeforeBroadcast();
            if (ev is IRabiPlayerMessage msg && msg.IsRabiPrivate()) {
                players = players.Where(p => p.id == msg.playerId);
            }
            foreach (var player in players) {
                ev.game.config.actionCenter.OnEvent(player.id, ev);
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<EventBase>(Send, EventPriority.Broadcast);
        }
    }
}