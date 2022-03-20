using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class LateClaimTileListener {
        public static Task ExecuteLateClaimTile(LateClaimTileEvent ev) {
            // Request player action
            var resolvers = GetClaimTileResolvers(ev.game);
            foreach (var resolver in resolvers) {
                resolver.Resolve(ev.player, null, ev.waitEvent.inquiry);
            }
            ev.waitEvent.inquiry.GetByPlayerId(ev.playerId).DisableSkip();
            DrawTileListener.AddActionHandler(ev.waitEvent, ev.reason);
            ev.bus.Queue(ev.waitEvent);
            return Task.CompletedTask;
        }

        private static IEnumerable<ResolverBase> GetClaimTileResolvers(Game game) {
            if (game.TryGet<PlayTileResolver>(out var resolver1)) {
                yield return resolver1;
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<LateClaimTileEvent>(ExecuteLateClaimTile, EventPriority.Execute);
        }
    }
}