using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System.Collections.Generic;
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
            var junEv = new IncreaseJunEvent(ev, ev.playerId);
            junEv.OnFinish(() => {
                // Request player action
                var resolvers = GetClaimTileResolvers(ev.game);
                foreach (var resolver in resolvers) {
                    resolver.Resolve(ev.player, null, ev.waitEvent.inquiry);
                }
                ev.waitEvent.inquiry.GetByPlayerId(ev.playerId).DisableSkip();
                DrawTileListener.AddActionHandler(ev.waitEvent, ev.reason);
                ev.bus.Queue(ev.waitEvent);
            });
            ev.bus.Queue(junEv);
            return Task.CompletedTask;
        }

        private static IEnumerable<ResolverBase> GetClaimTileResolvers(Game game) {
            if (game.TryGet<PlayTileResolver>(out var resolver1)) {
                yield return resolver1;
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<ClaimTileEvent>(ExecuteClaimTile, EventPriority.Execute);
        }
    }
}