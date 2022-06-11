using RabiRiichi.Communication;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Events {
    public static class EventBroadcast {
        public static Task Send(EventBase ev) {
            var players = ev.game.players.AsEnumerable();
            if (ev is IRabiPlayerMessage msg && msg.GetType().Has<RabiPrivateAttribute>()) {
                players = players.Where(p => p.id == msg.playerId);
            }
            foreach (var player in players) {
                ev.game.SendEvent(player.id, ev);
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<EventBase>(Send, EventPriority.Broadcast);
        }
    }
}