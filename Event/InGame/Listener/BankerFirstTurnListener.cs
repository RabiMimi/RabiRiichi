using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class BankerFirstTurnListener {

        public static Task ExecuteBankerFirstTurn(BankerFirstTurnEvent ev) {
            var freeTiles = ev.player.hand.freeTiles;
            var lastTile = freeTiles[^1];
            freeTiles.RemoveAt(freeTiles.Count - 1);
            foreach (var resolver in GetBankerFirstTurnResolvers(ev.game)) {
                resolver.Resolve(ev.player, lastTile, ev.waitEvent.inquiry);
            }
            ev.waitEvent.inquiry.GetByPlayerId(ev.playerId).DisableSkip();
            DrawTileListener.AddActionHandler(ev.waitEvent, DiscardReason.Draw);
            ev.Q.Queue(ev.waitEvent);
            return Task.CompletedTask;
        }

        private static IEnumerable<ResolverBase> GetBankerFirstTurnResolvers(Game game) {
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
            eventBus.Register<BankerFirstTurnEvent>(ExecuteBankerFirstTurn, EventPriority.Execute);
        }
    }
}