using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class KanListener {
        public static Task ExecuteKan(KanEvent ev) {
            // TODO: Add kan after chankan resolved
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
                    var waitEv = new WaitPlayerActionEvent(ev.game, inquiry);
                    ev.bus.Queue(waitEv);
                    AfterInquiry(waitEv).ConfigureAwait(false);
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }

        private static async Task AfterInquiry(WaitPlayerActionEvent waitEv) {
            await waitEv.WaitForFinish;
            var resp = waitEv.inquiry.responses;
            foreach (var action in resp) {
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