using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class WaitPlayerActionListener {
        public static Task Execute(WaitPlayerActionEvent e) {
            e.game.config.actionCenter.OnInquiry(e.inquiry);
            return e.inquiry.WaitForResponse;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<WaitPlayerActionEvent>(Execute, EventPriority.Execute);
        }
    }
}