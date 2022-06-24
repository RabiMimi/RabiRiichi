using RabiRiichi.Actions;
using RabiRiichi.Actions.Resolver;
using RabiRiichi.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
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
            e.tile = DrawFrom(e);
            var resolvers = GetDrawTileResolvers(e.game);
            foreach (var resolver in resolvers) {
                resolver.Resolve(e.player, e.tile, e.waitEvent.inquiry);
            }
            e.waitEvent.inquiry.GetByPlayerId(e.playerId).DisableSkip();
            AddActionHandler(e.waitEvent, e.source == TileSource.Wanpai
                ? DiscardReason.DrawRinshan : DiscardReason.Draw);
            e.Q.Queue(e.waitEvent);
            e.Q.Queue(new AddTileEvent(e));
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
                        new RiichiEvent(ev, action.playerId, option.tile, action.defaultTile, reason));
                } else {
                    eventBuilder.AddEvent(
                        new DiscardTileEvent(ev, action.playerId, option.tile, action.defaultTile, reason));
                }
            });
            ev.inquiry.AddHandler<KanAction>((action) => {
                var option = action.chosen as ChooseTilesActionOption;
                var kan = new Kan(option.tiles);
                eventBuilder.AddEvent(new KanEvent(ev, action.playerId, kan, option.tiles[^1]));
            });
            ev.inquiry.AddHandler<RyuukyokuAction>((action) => {
                eventBuilder.AddEvent(action.ev);
            });
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<DrawTileEvent>(DrawTile, EventPriority.Execute);
        }
    }
}
