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
                TileSource.Wall => e.game.wall.DrawRinshan(),// 抽岭上牌
                TileSource.Wanpai => e.game.wall.Draw(),// 从牌山随机选取一张牌
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
        }

        public static Task DrawTile(DrawTileEvent e) {
            var gameTile = DrawFrom(e);
            var resolvers = GetDrawTileResolvers(e.game);
            var inquiry = new MultiPlayerInquiry(e.game.info);
            foreach (var resolver in resolvers) {
                resolver.Resolve(e.player, gameTile, inquiry);
            }
            inquiry.GetByPlayerId(e.playerId).DisableSkip();
            var waitEv = new WaitPlayerActionEvent(e.game, inquiry);
            e.bus.Queue(waitEv);
            AfterPlayerAction(waitEv, e.reason).ConfigureAwait(false);
            return Task.CompletedTask;
        }

        public static async Task AfterPlayerAction(WaitPlayerActionEvent ev, DiscardReason reason) {
            try {
                await ev.WaitForFinish;
            } catch (OperationCanceledException) {
                return;
            }
            var resp = ev.inquiry.responses;
            var eventBuilder = new MultiEventBuilder();
            foreach (var action in resp) {
                if (action is TsumoAction tsumo) {
                    eventBuilder.AddAgari(ev.game, tsumo.playerId, tsumo.incoming, tsumo.agariInfo);
                } else if (action is PlayTileAction play) {
                    var option = play.chosen as ChooseTileActionOption;
                    if (action is RiichiAction) {
                        eventBuilder.AddEvent(
                            new RiichiEvent(ev.game, play.playerId, option.tile.gameTile, reason));
                    } else {
                        eventBuilder.AddEvent(
                            new DiscardTileEvent(ev.game, play.playerId, option.tile.gameTile, reason));
                    }
                } else if (action is KanAction kanAction) {
                    var option = kanAction.chosen as ChooseTilesActionOption;
                    var kan = new Kan(option.gameTiles);
                    eventBuilder.AddEvent(new KanEvent(ev.game, kanAction.playerId, kan, kanAction.incoming));
                }
            }
            eventBuilder.BuildAndQueue(ev.bus);
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<DrawTileEvent>(DrawTile, EventPriority.Execute);
        }
    }
}
