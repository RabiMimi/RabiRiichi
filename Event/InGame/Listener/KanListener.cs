using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class KanListener {
        public static Task ExecuteKan(KanEvent ev) {
            ev.Q.Queue(new IncreaseJunEvent(ev, ev.playerId));
            if (ev.kanSource != TileSource.DaiMinKan) {
                // 抢杠
                var resolvers = GetKanResolvers(ev.game);
                foreach (var resolver in resolvers) {
                    resolver.Resolve(ev.player, ev.incoming, ev.waitEvent.inquiry);
                }
            }
            ev.waitEvent.inquiry.AddHandler<RonAction>((action) => {
                ev.waitEvent.eventBuilder.AddAgari(ev.waitEvent, ev.playerId, ev.incoming, action.agariInfo);
            });
            ev.Q.Queue(ev.waitEvent);
            ev.waitEvent.OnFinish(() => {
                if (ev.waitEvent.responseEvents.Count > 0) {
                    // TODO: 处理抢杠后的情况：算作杠还是刻？
                    return;
                }
                // 没有人抢杠，继续处理开杠事件
                ev.Q.Queue(new AddKanEvent(ev));
            });
            return Task.CompletedTask;
        }

        private static IEnumerable<ResolverBase> GetKanResolvers(Game game) {
            if (game.TryGet<ChanKanResolver>(out var resolver)) {
                yield return resolver;
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<KanEvent>(ExecuteKan, EventPriority.Execute);
        }
    }
}