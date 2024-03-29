using RabiRiichi.Actions;
using RabiRiichi.Actions.Resolver;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
    public static class KanListener {
        public static Task ExecuteKan(KanEvent ev) {
            // Pretend that kan tile is discarded to resolve chankan, and recover state later
            using var _ = ev.incoming.Freeze();
            ev.player.hand.Play(ev.incoming, DiscardReason.Chankan);

            // Resolve chankan
            var resolvers = GetKanResolvers(ev.game, ev.kanSource);
            foreach (var resolver in resolvers) {
                resolver.Resolve(ev.player, ev.incoming, ev.waitEvent.inquiry);
            }
            ev.waitEvent.inquiry.AddHandler<RonAction>((action) => {
                ev.waitEvent.eventBuilder.AddAgari(ev.waitEvent, ev.playerId, ev.incoming, action.agariInfo);
            });
            ev.Q.Queue(ev.waitEvent);
            ev.waitEvent.OnFinish += () => {
                // 若有人抢杠，杠在荣和后成立
                var addKanEvent = new AddKanEvent(ev);
                ev.Q.Queue(addKanEvent);
                // 若游戏进入下一局，不要执行添加杠事件
                ev.bus.Subscribe<NextGameEvent>((e) => {
                    addKanEvent.Cancel();
                    return Task.CompletedTask;
                }, EventPriority.After, 1);
            };
            return Task.CompletedTask;
        }

        private static IEnumerable<ResolverBase> GetKanResolvers(Game game, TileSource kanSource) {
            var resolver = kanSource switch {
                TileSource.Ankan => game.Get<ChanAnkanResolver>(),
                TileSource.Kakan => game.Get<ChankanResolver>(),
                _ => null
            };
            if (resolver != null) {
                yield return resolver;
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<KanEvent>(ExecuteKan, EventPriority.Execute);
        }
    }
}