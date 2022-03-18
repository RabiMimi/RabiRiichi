using RabiIppatsu.Event.InGame;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class SetIppatsuListener {
        public static Task ExecuteIppatsu(SetIppatsuEvent ev) {
            ev.player.hand.ippatsu = ev.ippatsu;
            return Task.CompletedTask;
        }

        public static Task AfterRiichi(SetRiichiEvent ev) {
            ev.bus.Queue(new SetIppatsuEvent(ev.game, ev.playerId, true));
            return Task.CompletedTask;
        }

        public static Task ResetIppatsu(DiscardTileEvent ev) {
            if (ev.player.hand.ippatsu) {
                ev.bus.Queue(new SetIppatsuEvent(ev.game, ev.playerId, false));
            }
            return Task.CompletedTask;
        }

        public static Task ResetAllIppatsu(EventBase ev) {
            foreach (var player in ev.game.players.Where(p => p.hand.ippatsu)) {
                ev.bus.Queue(new SetIppatsuEvent(ev.game, player.id, false));
            }
            return Task.CompletedTask;
        }

        public static Task ResetAllKan(KanEvent ev) {
            // 由于可能会枪杠，将取消一发的时点延迟至抽岭上牌时
            new EventListener<DrawTileEvent>(ev.bus)
                .LatePrepare((_) => {
                    ResetAllIppatsu(ev);
                    return Task.CompletedTask;
                }, 1)
                .ScopeTo(EventScope.Game);
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<SetIppatsuEvent>(ExecuteIppatsu, EventPriority.Execute);
            eventBus.Register<SetRiichiEvent>(AfterRiichi, EventPriority.After + 10);
            eventBus.Register<DiscardTileEvent>(ResetIppatsu, EventPriority.After + 10);
            eventBus.Register<ClaimTileEvent>(ResetAllIppatsu, EventPriority.After + 10);
            eventBus.Register<KanEvent>(ResetAllKan, EventPriority.After + 10);
        }
    }
}