using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class KanListener {
        public static Task ExecuteKan(KanEvent ev) {
            ev.Q.Queue(new IncreaseJunEvent(ev, ev.playerId));
            var resolvers = GetKanResolvers(ev.game, ev.kanSource);
            foreach (var resolver in resolvers) {
                resolver.Resolve(ev.player, ev.incoming, ev.waitEvent.inquiry);
            }
            ev.waitEvent.inquiry.AddHandler<RonAction>((action) => {
                ev.waitEvent.eventBuilder.AddAgari(ev.waitEvent, ev.playerId, ev.incoming, action.agariInfo);
            });
            ev.Q.Queue(ev.waitEvent);
            ev.waitEvent.OnFinish += () => {
                if (ev.waitEvent.responseEvents.Count > 0) {
                    // TODO: 处理抢杠后的情况：算作杠还是刻？
                    return;
                }
                // 没有人抢杠，继续处理开杠事件
                ev.Q.Queue(new AddKanEvent(ev));
            };
            return Task.CompletedTask;
        }

        private static IEnumerable<ResolverBase> GetKanResolvers(Game game, TileSource kanSource) {
            if (kanSource != TileSource.DaiMinKan && game.TryGet<ChanKanResolver>(out var resolver1)) {
                // 抢杠
                yield return resolver1;
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<KanEvent>(ExecuteKan, EventPriority.Execute);
        }
    }
}