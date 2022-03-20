using RabiRiichi.Riichi;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class ClaimTileListener {
        public static Task ExecuteClaimTile(ClaimTileEvent ev) {
            ev.bus.Queue(new SetMenzenEvent(ev, ev.playerId, false));
            if (ev.group is Kan kan) {
                // 杠会增加巡目，在此跳过
                ev.bus.Queue(new KanEvent(ev, ev.playerId, kan, ev.tile));
                return Task.CompletedTask;
            }

            // Check discard reason
            if (ev.group is Shun shun) {
                ev.player.hand.AddChii(shun);
                ev.reason = DiscardReason.Chii;
            } else if (ev.group is Kou kou) {
                ev.player.hand.AddPon(kou);
                ev.reason = DiscardReason.Pon;
            } else {
                // Unknown group
                return Task.CompletedTask;
            }

            // Increase jun
            ev.bus.Queue(new IncreaseJunEvent(ev, ev.playerId));
            ev.bus.Queue(new LateClaimTileEvent(ev));
            return Task.CompletedTask;
        }
        public static void Register(EventBus eventBus) {
            eventBus.Register<ClaimTileEvent>(ExecuteClaimTile, EventPriority.Execute);
        }
    }
}