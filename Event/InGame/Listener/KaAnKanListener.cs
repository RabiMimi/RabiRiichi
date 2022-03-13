using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class KaAnKanListener {
        public static Task ExecuteKaAnKan(KaAnKanEvent ev) {
            if (ev.isAnKan) {
                ev.player.hand.AddKan(ev.kan);
            } else {
                ev.player.hand.KaKan(ev.kan);
            }
            // TODO: Use RonResolver to resolve possible ChanKan and schedule next event
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<KaAnKanEvent>(ExecuteKaAnKan, EventPriority.Execute);
        }
    }
}