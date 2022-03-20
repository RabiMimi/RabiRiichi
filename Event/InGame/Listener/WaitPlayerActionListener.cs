using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class WaitPlayerActionListener {
        public static async Task ExecuteWaitPlayer(WaitPlayerActionEvent e) {
            if (e.inquiry.IsEmpty) {
                return;
            }
            e.game.config.actionCenter.OnInquiry(e.inquiry);
            await e.inquiry.WaitForFinish;
            if (e.eventBuilder != null) {
                e.responseEvents.AddRange(e.eventBuilder.BuildAndQueue(e.bus));
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<WaitPlayerActionEvent>(ExecuteWaitPlayer, EventPriority.Execute);
        }
    }
}