using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class KanListener {
        public static Task ExecuteKan(KanEvent ev) {
            if (ev.kanSource == TileSource.AnKan || ev.kanSource == TileSource.DaiMinKan) {
                ev.player.hand.AddKan(ev.kan);
            } else if (ev.kanSource == TileSource.KaKan) {
                ev.player.hand.KaKan(ev.kan);
            } else {
                throw new InvalidDataException($"Invalid kanSource: {ev.kanSource}");
            }
            if (ev.kanSource != TileSource.DaiMinKan) {
                // 抢杠
                if (ev.game.TryGet<ChanKanResolver>(out var resolver)) {
                    var inquiry = new MultiPlayerInquiry(ev.game.info);
                    resolver.Resolve(ev.player, ev.incoming, inquiry);
                    ev.game.eventBus.Queue(new WaitPlayerActionEvent(ev.game, inquiry));
                    AfterInquiry(inquiry).ConfigureAwait(false);
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }

        private static async Task AfterInquiry(MultiPlayerInquiry inquiry) {
            await inquiry.WaitForFinish;
            foreach (var action in inquiry.responses) {
                if (action is RonAction) {
                    // TODO: 和了
                }
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<KanEvent>(ExecuteKan, EventPriority.Execute);
        }
    }
}