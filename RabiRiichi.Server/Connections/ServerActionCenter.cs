using RabiRiichi.Actions;
using RabiRiichi.Communication;
using RabiRiichi.Events;
using RabiRiichi.Generated.Actions;
using RabiRiichi.Generated.Events;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Connections {
  public class ServerActionCenter(Room room) : IActionCenter {
    private class InquiryContext(MultiPlayerInquiry inquiry) {
      public readonly MultiPlayerInquiry inquiry = inquiry;
      public readonly DateTime startTime = DateTime.UtcNow;
    }

    private readonly Room room = room;
    private InquiryContext context;

    private void EndInquiry(InquiryContext context) {
      var oldContext = Interlocked.CompareExchange(ref this.context, null, context);
      if (oldContext != context) {
        return;
      }
      oldContext.inquiry.Finish();
    }

    public void OnEvent(int seat, EventBase ev) {
      room.GetPlayerBySeat(seat)?.OnEvent(ev);
    }

    private void SendInquiry(InquiryContext ctx, int seat) {
      var inquiry = ctx?.inquiry.GetByPlayerId(seat);
      if (inquiry == null) {
        return;
      }
      var player = room.GetPlayerBySeat(seat);
      if (player == null) {
        return;
      }
      var remaining = TimeSpan.Zero;
      if (ctx.inquiry.timeout > TimeSpan.Zero) {
        var elapsed = DateTime.UtcNow - ctx.startTime;
        remaining = ctx.inquiry.timeout - elapsed;
        if (remaining < TimeSpan.Zero) {
          remaining = TimeSpan.Zero;
        }
      }
      player.OnInquiry(ctx.inquiry, inquiry, remaining, resp => {
        if (ctx.inquiry.OnResponse(resp)) {
          EndInquiry(ctx);
        }
      });
    }

    public void SyncInquiryTo(int seat) {
      SendInquiry(context, seat);
    }

    public void OnInquiry(MultiPlayerInquiry inquiry) {
      var ctx = new InquiryContext(inquiry);
      // If a previous inquiry has already finished (hasExecuted), clear the stale context
      // before trying to register the new one. This prevents the “Inquiry already in progress”
      // race condition.
      var currentContext = context;
      if (currentContext != null && currentContext.inquiry.hasExecuted) {
        Interlocked.CompareExchange(ref context, null, currentContext);
      }
      if (Interlocked.CompareExchange(ref context, ctx, null) != null) {
        throw new InvalidOperationException("Inquiry already in progress");
      }
      for (int i = 0; i < inquiry.playerInquiries.Count; i++) {
        var playerInquiry = inquiry.playerInquiries[i];
        SendInquiry(ctx, playerInquiry.playerId);
      }
      if (inquiry.IsEmpty) {
        EndInquiry(ctx);
      }
    }
  }
}