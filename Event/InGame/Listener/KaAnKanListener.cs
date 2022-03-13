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
            var inquiry = new MultiPlayerInquiry(ev.game.info);
            ev.game.Get<ChanKanResolver>().Resolve(ev.player, ev.incoming, inquiry);
            ev.game.eventBus.Queue(new WaitPlayerActionEvent(ev.game, inquiry));
            AfterInquiry(inquiry).ConfigureAwait(false);
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