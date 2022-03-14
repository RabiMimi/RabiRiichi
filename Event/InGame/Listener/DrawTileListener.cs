using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class DrawTileListener {

        public static Task PrepareTile(DrawTileEvent e) {
            if (!e.tile.IsEmpty) {
                // 已经有牌了，不需要再摸牌
                return Task.CompletedTask;
            }
            // 从牌山随机选取一张牌
            var yama = e.game.wall;
            if (!yama.Has(1)) {
                e.Cancel();
                return Task.CompletedTask;
            }
            e.tile = yama.SelectOne();
            return Task.CompletedTask;
        }

        private static IEnumerable<ResolverBase> GetDrawTileResolvers(Game game) {
            if (game.TryGet<PlayTileResolver>(out var resolver1)) {
                yield return resolver1;
            }
            if (game.TryGet<RiichiResolver>(out var resolver2)) {
                yield return resolver2;
            }
            if (game.TryGet<KanResolver>(out var resolver3)) {
                yield return resolver3;
            }
            if (game.TryGet<TsumoResolver>(out var resolver4)) {
                yield return resolver4;
            }
        }

        public static Task DrawTile(DrawTileEvent e) {
            e.game.wall.Draw(e.tile);
            var resolvers = GetDrawTileResolvers(e.game);
            var inquiry = new MultiPlayerInquiry(e.game.info);
            var incoming = new GameTile(e.tile) {
                source = e.source,
            };
            foreach (var resolver in resolvers) {
                resolver.Resolve(e.player, incoming, inquiry);
            }
            e.game.eventBus.Queue(new WaitPlayerActionEvent(e.game, inquiry));
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<DrawTileEvent>(PrepareTile, EventPriority.Prepare);
            eventBus.Register<DrawTileEvent>(DrawTile, EventPriority.Execute);
        }
    }
}
