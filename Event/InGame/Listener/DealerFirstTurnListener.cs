using RabiRiichi.Action.Resolver;
using RabiRiichi.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class DealerFirstTurnListener {

        public static Task ExecuteDealerFirstTurn(DealerFirstTurnEvent ev) {
            var freeTiles = ev.player.hand.freeTiles;
            var incoming = ev.incoming;
            freeTiles.Remove(incoming);
            incoming.player = null;
            incoming.source = TileSource.Wall;
            foreach (var resolver in GetDealerFirstTurnResolvers(ev.game)) {
                resolver.Resolve(ev.player, incoming, ev.waitEvent.inquiry);
            }
            ev.waitEvent.inquiry.GetByPlayerId(ev.playerId).DisableSkip();
            DrawTileListener.AddActionHandler(ev.waitEvent, DiscardReason.Draw);
            ev.Q.Queue(ev.waitEvent);
            ev.Q.Queue(new AddTileEvent(ev, incoming));
            return Task.CompletedTask;
        }

        private static IEnumerable<ResolverBase> GetDealerFirstTurnResolvers(Game game) {
            if (game.TryGet<PlayTileResolver>(out var resolver1)) {
                yield return resolver1;
            }
            if (game.TryGet<RiichiResolver>(out var resolver2)) {
                yield return resolver2;
            }
            if (game.TryGet<KanResolver>(out var resolver3)) {
                yield return resolver3;
            }
            if (game.TryGet<TenhouResolver>(out var resolver4)) {
                yield return resolver4;
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<DealerFirstTurnEvent>(ExecuteDealerFirstTurn, EventPriority.Execute);
        }
    }
}