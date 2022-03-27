using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class SetIppatsuListener {
        public static Task ExecuteIppatsu(SetIppatsuEvent ev) {
            ev.player.hand.ippatsu = ev.ippatsu;
            return Task.CompletedTask;
        }

        public static Task AfterRiichi(SetRiichiEvent ev) {
            ev.Q.Queue(new SetIppatsuEvent(ev, ev.playerId, true));
            return Task.CompletedTask;
        }

        public static Task ResetIppatsu(DiscardTileEvent ev) {
            if (ev.player.hand.ippatsu) {
                ev.Q.Queue(new SetIppatsuEvent(ev, ev.playerId, false));
            }
            return Task.CompletedTask;
        }

        public static Task ResetAllIppatsu(EventBase ev) {
            foreach (var player in ev.game.players.Where(p => p.hand.ippatsu)) {
                ev.Q.Queue(new SetIppatsuEvent(ev, player.id, false));
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<SetIppatsuEvent>(ExecuteIppatsu, EventPriority.Execute);
            eventBus.Subscribe<SetRiichiEvent>(AfterRiichi, EventPriority.After + 10);
            eventBus.Subscribe<DiscardTileEvent>(ResetIppatsu, EventPriority.After + 10);
            eventBus.Subscribe<ClaimTileEvent>(ResetAllIppatsu, EventPriority.After + 10);
            eventBus.Subscribe<AddKanEvent>(ResetAllIppatsu, EventPriority.After + 10);
        }
    }
}