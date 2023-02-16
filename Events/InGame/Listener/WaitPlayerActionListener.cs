using RabiRiichi.Core;
using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
    public static class WaitPlayerActionListener {
        public static async Task ExecuteWaitPlayer(WaitPlayerActionEvent e) {
            e.game.SendInquiry(e.inquiry);
            try {
                e.bus.eventProcessingLock.Release();
                await e.inquiry.WaitForFinish;
            } finally {
                e.bus.eventProcessingLock.Lock(EventBus.EVENT_PROCESSING_TIMEOUT);
            }
            foreach (var playerInquiry in e.inquiry.playerInquiries) {
                e.Q.Queue(new EndInquiryEvent(e, playerInquiry.playerId));
            }
            if (e.eventBuilder != null) {
                e.responseEvents.AddRange(e.eventBuilder.BuildAndQueue(e.Q));
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<WaitPlayerActionEvent>(ExecuteWaitPlayer, EventPriority.Execute);
        }
    }
}