using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class WaitPlayerActionListener {
        public static Task BeforeWaitPlayer(WaitPlayerActionEvent e) {
            e.inquiry.BeforeBroadcast();
            return Task.CompletedTask;
        }

        public static async Task ExecuteWaitPlayer(WaitPlayerActionEvent e) {
            if (e.inquiry.IsEmpty) {
                return;
            }
            e.game.SendInquiry(e.inquiry);
            await e.inquiry.WaitForFinish;
            if (e.eventBuilder != null) {
                e.responseEvents.AddRange(e.eventBuilder.BuildAndQueue(e.Q));
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<WaitPlayerActionEvent>(BeforeWaitPlayer, EventPriority.Prepare);
            eventBus.Subscribe<WaitPlayerActionEvent>(ExecuteWaitPlayer, EventPriority.Execute);
        }
    }
}