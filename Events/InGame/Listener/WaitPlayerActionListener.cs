using RabiRiichi.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
  public static class WaitPlayerActionListener {
    public static async Task ExecuteWaitPlayer(WaitPlayerActionEvent e) {
      e.inquiry.timeout = e.timeout;
      e.game.SendInquiry(e.inquiry);
      CancellationTokenSource cts = null;
      if (e.timeout > TimeSpan.Zero) {
        cts = new CancellationTokenSource();
        var token = cts.Token;
        _ = Task.Delay(e.timeout, token).ContinueWith(t => {
          if (t.IsCompletedSuccessfully) {
            e.inquiry.Finish();
          }
        }, TaskScheduler.Default);
      }
      try {
        e.bus.eventProcessingLock.Release();
        await e.inquiry.WaitForFinish;
      } finally {
        if (cts != null) {
          cts.Cancel();
          cts.Dispose();
        }
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