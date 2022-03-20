using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class ClaimTileListener {
        public static Task ExecuteClaimTile(ClaimTileEvent ev) {
            ev.bus.Queue(new SetMenzenEvent(ev.game, ev.playerId, false));
            if (ev.group is Kan kan) {
                // 杠会增加巡目，在此跳过
                ev.bus.Queue(new KanEvent(ev.game, ev.playerId, kan, ev.tile));
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
            var junEv = new IncreaseJunEvent(ev.game, ev.playerId);
            ev.bus.Queue(junEv);
            AfterIncreaseJun(junEv, ev).ConfigureAwait(false);
            return Task.CompletedTask;
        }

        private static IEnumerable<ResolverBase> GetClaimTileResolvers(Game game) {
            if (game.TryGet<PlayTileResolver>(out var resolver1)) {
                yield return resolver1;
            }
        }

        private static async Task AfterIncreaseJun(IncreaseJunEvent ev, ClaimTileEvent claimEv) {
            try {
                await ev.WaitForFinish;
            } catch (OperationCanceledException) {
                return;
            }
            // Request player action
            var resolvers = GetClaimTileResolvers(ev.game);
            foreach (var resolver in resolvers) {
                resolver.Resolve(ev.player, null, claimEv.inquiry);
            }
            claimEv.inquiry.GetByPlayerId(ev.playerId).DisableSkip();
            var waitEv = new WaitPlayerActionEvent(ev.game, claimEv.inquiry);
            ev.bus.Queue(waitEv);
            await DrawTileListener.AfterPlayerAction(waitEv, claimEv.reason);
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<ClaimTileEvent>(ExecuteClaimTile, EventPriority.Execute);
        }
    }
}