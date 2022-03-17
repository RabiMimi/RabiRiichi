using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class ClaimTileListener {
        public static Task ExecuteClaimTile(ClaimTileEvent ev) {
            var junEv = new IncreaseJunEvent(ev.game, ev.playerId);
            ev.bus.Queue(junEv);
            if (ev.group is Kan kan) {
                ev.bus.Queue(new KanEvent(ev.game, ev.playerId, kan, ev.tile));
                return Task.CompletedTask;
            }
            DiscardReason reason;
            if (ev.group is Shun shun) {
                ev.player.hand.AddChi(shun);
                reason = DiscardReason.Chi;
            } else if (ev.group is Kou kou) {
                ev.player.hand.AddPon(kou);
                reason = DiscardReason.Pon;
            } else {
                return Task.CompletedTask;
            }
            AfterIncreaseJun(junEv, reason).ConfigureAwait(false);
            return Task.CompletedTask;
        }

        private static IEnumerable<ResolverBase> GetClaimTileResolvers(Game game) {
            if (game.TryGet<PlayTileResolver>(out var resolver1)) {
                yield return resolver1;
            }
        }

        private static async Task AfterIncreaseJun(IncreaseJunEvent ev, DiscardReason reason) {
            try {
                await ev.WaitForFinish;
            } catch (TaskCanceledException) {
                return;
            }
            var resolvers = GetClaimTileResolvers(ev.game);
            var inquiry = new MultiPlayerInquiry(ev.game.info);
            foreach (var resolver in resolvers) {
                resolver.Resolve(ev.player, null, inquiry);
            }
            inquiry.GetByPlayerId(ev.playerId).DisableSkip();
            var waitEv = new WaitPlayerActionEvent(ev.game, inquiry);
            ev.bus.Queue(waitEv);
            await DrawTileListener.AfterPlayerAction(waitEv, reason);
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<ClaimTileEvent>(ExecuteClaimTile, EventPriority.Execute);
        }
    }
}