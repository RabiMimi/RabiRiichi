using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class WaitPlayerActionListener {
        public static Task Execute(WaitPlayerActionEvent e) {
            if (e.inquiry.IsEmpty) {
                return Task.CompletedTask;
            }
            e.game.config.actionCenter.OnInquiry(e.inquiry);
            return e.inquiry.WaitForFinish;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<WaitPlayerActionEvent>(Execute, EventPriority.Execute);
        }
    }
}