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
                ev.player.hand.AddKan(kan);
                ev.bus.Queue(new DrawTileEvent(ev.game, ev.playerId, TileSource.Wanpai));
                return Task.CompletedTask;
            }
            if (ev.group is Shun shun) {
                ev.player.hand.AddChi(shun);
            } else if (ev.group is Kou kou) {
                ev.player.hand.AddPon(kou);
            }
            AfterIncreaseJun(junEv).ConfigureAwait(false);
            return Task.CompletedTask;
        }

        private static IEnumerable<ResolverBase> GetClaimTileResolvers(Game game) {
            if (game.TryGet<PlayTileResolver>(out var resolver1)) {
                yield return resolver1;
            }
        }

        private static async Task AfterIncreaseJun(IncreaseJunEvent ev) {
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
            await DrawTileListener.AfterPlayerAction(waitEv);
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<ClaimTileEvent>(ExecuteClaimTile, EventPriority.Execute);
        }
    }
}