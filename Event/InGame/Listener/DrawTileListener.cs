using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class DrawTileListener {

        private static GameTile DrawFrom(DrawTileEvent e) {
            return e.source switch {
                TileSource.Wanpai => e.game.wall.DrawRinshan(),// 抽岭上牌
                TileSource.Wall => e.game.wall.Draw(),// 从牌山随机选取一张牌
                _ => throw new ArgumentException($"Drawing from unsupported tile source: {e.source}"),
            };
        }

        public static IEnumerable<ResolverBase> GetDrawTileResolvers(Game game) {
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
            if (game.TryGet<RyuukyokuResolver>(out var resolver5)) {
                yield return resolver5;
            }
        }

        public static Task DrawTile(DrawTileEvent e) {
            var gameTile = DrawFrom(e);
            var resolvers = GetDrawTileResolvers(e.game);
            foreach (var resolver in resolvers) {
                resolver.Resolve(e.player, gameTile, e.waitEvent.inquiry);
            }
            e.waitEvent.inquiry.GetByPlayerId(e.playerId).DisableSkip();
            AddActionHandler(e.waitEvent, e.reason);
            e.Q.Queue(e.waitEvent);
            return Task.CompletedTask;
        }

        public static void AddActionHandler(WaitPlayerActionEvent ev, DiscardReason reason) {
            var eventBuilder = ev.eventBuilder;
            ev.inquiry.AddHandler<TsumoAction>((action) => {
                eventBuilder.AddAgari(ev, action.playerId, action.incoming, action.agariInfo);
            });
            ev.inquiry.AddHandler<PlayTileAction>((action) => {
                var option = action.chosen as ChooseTileActionOption;
                if (action is RiichiAction) {
                    eventBuilder.AddEvent(
                        new RiichiEvent(ev, action.playerId, option.tile, reason));
                } else {
                    eventBuilder.AddEvent(
                        new DiscardTileEvent(ev, action.playerId, option.tile, reason));
                }
            });
            ev.inquiry.AddHandler<KanAction>((action) => {
                var option = action.chosen as ChooseTilesActionOption;
                var kan = new Kan(option.tiles);
                eventBuilder.AddEvent(new KanEvent(ev, action.playerId, kan, action.incoming));
            });
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<DrawTileEvent>(DrawTile, EventPriority.Execute);
        }
    }
}
