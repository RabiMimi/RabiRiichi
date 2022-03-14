using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class KaAnKanListener {
        public static Task ExecuteKaAnKan(KaAnKanEvent ev) {
            if (ev.isAnKan) {
                ev.player.hand.AddKan(ev.kan);
            } else {
                ev.player.hand.KaKan(ev.kan);
            }
            if (ev.game.TryGet<ChanKanResolver>(out var resolver)) {
                var inquiry = new MultiPlayerInquiry(ev.game.info);
                resolver.Resolve(ev.player, ev.incoming, inquiry);
                ev.game.eventBus.Queue(new WaitPlayerActionEvent(ev.game, inquiry));
                AfterInquiry(inquiry).ConfigureAwait(false);
            } else {
                // TODO: Next event
            }
            return Task.CompletedTask;
        }

        private static async Task AfterInquiry(MultiPlayerInquiry inquiry) {
            await inquiry.WaitForResponse;
            foreach (var action in inquiry.responses) {
                if (action is RonAction) {
                    // TODO: 和了
                }
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<KaAnKanEvent>(ExecuteKaAnKan, EventPriority.Execute);
        }
    }
}