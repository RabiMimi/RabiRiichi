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
            if (ev.kanSource != TileSource.DaiMinKan) {
                // 抢杠
                var resolvers = GetKanResolvers(ev.game);
                foreach (var resolver in resolvers) {
                    resolver.Resolve(ev.player, ev.incoming, ev.waitEvent.inquiry);
                }
            }
            ev.bus.Queue(ev.waitEvent);
            AfterChanKan(ev.waitEvent, ev).ConfigureAwait(false);
            return Task.CompletedTask;
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
                // TODO: 处理抢杠后的情况：算作杠还是刻？
                return;
            }

            // 没有人抢杠，继续处理开杠事件
            waitEv.bus.Queue(new AddKanEvent(kanEv));
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<KanEvent>(ExecuteKan, EventPriority.Execute);
        }
    }
}