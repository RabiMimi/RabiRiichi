﻿using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class DrawTileListener {

        public static Task PrepareTile(DrawTileEvent e) {
            if (e.tile != null) {
                // 已经有牌了，不需要再摸牌
                return Task.CompletedTask;
            }
            switch (e.source) {
                case TileSource.Wall:
                case TileSource.Wanpai:
                    // 从牌山随机选取一张牌
                    if (!e.game.wall.Has(1)) {
                        e.Cancel();
                        return Task.CompletedTask;
                    }
                    e.tile = new GameTile(e.game.wall.SelectOne()) {
                        source = e.source
                    };
                    break;
                default:
                    throw new ArgumentException($"Drawing from unsupported tile source: {e.source}");
            }
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
            e.game.wall.Draw(e.tile.tile);
            var resolvers = GetDrawTileResolvers(e.game);
            var inquiry = new MultiPlayerInquiry(e.game.info);
            foreach (var resolver in resolvers) {
                resolver.Resolve(e.player, e.tile, inquiry);
            }
            inquiry.GetByPlayerId(e.playerId).DisableSkip();
            var waitEv = new WaitPlayerActionEvent(e.game, inquiry);
            e.bus.Queue(waitEv);
            AfterPlayerAction(waitEv).ConfigureAwait(false);
            return Task.CompletedTask;
        }

        private static async Task AfterPlayerAction(WaitPlayerActionEvent ev) {
            await ev.WaitForFinish;
            var resp = ev.inquiry.responses;
            foreach (var action in resp) {
                if (action is TsumoAction tsumo) {
                    ev.bus.Queue(new AgariEvent(ev.game,
                        new AgariInfoList(tsumo.playerId, tsumo.incoming, tsumo.agariInfo)));
                } else if (action is PlayTileAction play) {
                    var option = play.chosen as ChooseTileActionOption;
                    if (action is RiichiAction) {
                        ev.bus.Queue(
                            new RiichiEvent(ev.game, play.playerId, option.tile.gameTile));
                    } else {
                        ev.bus.Queue(
                            new DiscardTileEvent(ev.game, play.playerId, option.tile.gameTile));
                    }
                } else if (action is KanAction kanAction) {
                    var option = kanAction.chosen as ChooseTilesActionOption;
                    var kan = new Kan(option.gameTiles);
                    ev.bus.Queue(new KanEvent(ev.game, kanAction.playerId, kan, kanAction.incoming));
                }
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<DrawTileEvent>(PrepareTile, EventPriority.Prepare);
            eventBus.Register<DrawTileEvent>(DrawTile, EventPriority.Execute);
        }
    }
}
