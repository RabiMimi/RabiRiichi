using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class DiscardTileListener {
        public static Task ExecuteDiscardTile(DiscardTileEvent ev) {
            ev.player.hand.Play(ev.tile, ev.reason);
            var inquiry = new MultiPlayerInquiry(ev.game.info);
            var resolvers = GetDiscardTileResolvers(ev.game);
            foreach (var resolver in resolvers) {
                resolver.Resolve(ev.player, ev.tile, inquiry);
            }
            var waitEv = new WaitPlayerActionEvent(ev.game, inquiry);
            ev.bus.Queue(waitEv);
            AfterPlayerAction(waitEv, ev.tile).ConfigureAwait(false);
            return Task.CompletedTask;
        }

        public static async Task AfterPlayerAction(WaitPlayerActionEvent ev, GameTile incoming) {
            try {
                await ev.WaitForFinish;
            } catch (TaskCanceledException) {
                return;
            }
            var eventBuilder = new MultiEventBuilder();
            var resp = ev.inquiry.responses;
            foreach (var action in resp) {
                if (action is ChiAction chi) {
                    var option = (ChooseTilesActionOption)chi.chosen;
                    eventBuilder.AddEvent(new ClaimTileEvent(ev.game, chi.playerId, new Shun(option.gameTiles), incoming));
                } else if (action is PonAction pon) {
                    var option = (ChooseTilesActionOption)pon.chosen;
                    eventBuilder.AddEvent(new ClaimTileEvent(ev.game, pon.playerId, new Kou(option.gameTiles), incoming));
                } else if (action is KanAction kan) {
                    var option = (ChooseTilesActionOption)kan.chosen;
                    eventBuilder.AddEvent(new ClaimTileEvent(ev.game, kan.playerId, new Kan(option.gameTiles), incoming));
                } else if (action is RonAction ron) {
                    eventBuilder.AddAgari(ev.game, incoming.player.id, incoming, ron.agariInfo);
                }
            }
            // TODO: Riichi
            if (eventBuilder.BuildAndQueue(ev.bus).Count == 0) {
                ev.bus.Queue(new NextPlayerEvent(ev.game, ev.playerId));
            }
        }

        private static IEnumerable<ResolverBase> GetDiscardTileResolvers(Game game) {
            if (game.TryGet<ChiResolver>(out var resolver1)) {
                yield return resolver1;
            }
            if (game.TryGet<PonResolver>(out var resolver2)) {
                yield return resolver2;
            }
            if (game.TryGet<KanResolver>(out var resolver3)) {
                yield return resolver3;
            }
            if (game.TryGet<RonResolver>(out var resolver4)) {
                yield return resolver4;
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<DiscardTileEvent>(ExecuteDiscardTile, EventPriority.Execute);
        }
    }
}