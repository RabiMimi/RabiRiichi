using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class KanListener {
        public static Task ExecuteKan(KanEvent ev) {
            ev.bus.Queue(new IncreaseJunEvent(ev.game, ev.playerId));
            var inquiry = new MultiPlayerInquiry(ev.game.info);
            if (ev.kanSource != TileSource.DaiMinKan) {
                // 抢杠
                var resolvers = GetKanResolvers(ev.game);
                foreach (var resolver in resolvers) {
                    resolver.Resolve(ev.player, ev.incoming, inquiry);
                }
            }
            var waitEv = new WaitPlayerActionEvent(ev.game, inquiry);
            ev.bus.Queue(waitEv);
            AfterChanKan(waitEv, ev).ConfigureAwait(false);
            return Task.CompletedTask;
        }

        private static void FinishKan(KanEvent ev) {
            if (ev.kanSource == TileSource.AnKan || ev.kanSource == TileSource.DaiMinKan) {
                ev.player.hand.AddKan(ev.kan);
            } else if (ev.kanSource == TileSource.KaKan) {
                ev.player.hand.KaKan(ev.kan);
            } else {
                return;
            }

            if (ev.kanSource == TileSource.AnKan) {
                ev.bus.Queue(new RevealDoraEvent(ev.game, ev.playerId));
            } else {
                new EventListener<DiscardTileEvent>(ev.bus)
                    .LatePrepare((_) => {
                        ev.bus.Queue(new RevealDoraEvent(ev.game, ev.playerId));
                        return Task.CompletedTask;
                    }, 1)
                    .CancelOn<IncreaseJunEvent>()
                    .ScopeTo(EventScope.Game);
            }

            ev.bus.Queue(new DrawTileEvent(ev.game, ev.playerId, TileSource.Wanpai, DiscardReason.DrawRinshan));
        }

        private static IEnumerable<ResolverBase> GetKanResolvers(Game game) {
            if (game.TryGet<ChanKanResolver>(out var resolver)) {
                yield return resolver;
            }
        }

        private static async Task AfterChanKan(WaitPlayerActionEvent waitEv, KanEvent kanEv) {
            try {
                await waitEv.WaitForFinish;
            } catch (OperationCanceledException) {
                return;
            }
            var eventBuilder = new MultiEventBuilder();
            var resp = waitEv.inquiry.responses;
            foreach (var action in resp) {
                if (action is RonAction ron) {
                    eventBuilder.AddAgari(waitEv.game, kanEv.playerId, kanEv.incoming, ron.agariInfo);
                }
            }
            if (eventBuilder.BuildAndQueue(waitEv.bus).Count > 0) {
                return;
            }

            // 没有人抢杠，继续处理开杠事件
            FinishKan(kanEv);
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<KanEvent>(ExecuteKan, EventPriority.Execute);
        }
    }
}