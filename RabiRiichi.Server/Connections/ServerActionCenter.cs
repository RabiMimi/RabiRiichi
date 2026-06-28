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
    }

    private readonly Room room = room;
    private InquiryContext context;

    private ServerMessageWrapper SendMessage(int seat, ServerMessageDto msg) {
      return room.GetPlayerBySeat(seat)?.connection.Queue(msg);
    }

    private void EndInquiry(InquiryContext context) {
      var oldContext = Interlocked.CompareExchange(ref this.context, null, context);
      if (oldContext != context) {
        return;
      }
      oldContext.inquiry.Finish();
    }

    public void OnEvent(int seat, EventBase ev) {
      var proto = ev.game.SerializeProto<EventMsg>(ev, seat);
      if (proto != null) {
        SendMessage(seat, proto.CreateDto());
      }
    }

    private ServerMessageWrapper SendInquiry(InquiryContext ctx, int seat) {
      var inquiry = ctx?.inquiry.GetByPlayerId(seat);
      if (inquiry == null) {
        return null;
      }
      var singlePlayerInquiryMsg = ctx.inquiry.game.SerializeProto<SinglePlayerInquiryMsg>(inquiry, seat);
      if (singlePlayerInquiryMsg != null && ctx.inquiry.timeout > TimeSpan.Zero) {
        double clientTimeout = ctx.inquiry.timeout.TotalSeconds;
        singlePlayerInquiryMsg.TimeoutSeconds = clientTimeout;
      }
      var inquiryMsg = new ServerInquiryMsg {
        Inquiry = singlePlayerInquiryMsg
      };
      var msg = SendMessage(seat, ProtoUtils.CreateDto(inquiryMsg));
      if (msg != null) {
        async Task WaitResponse() {
          var waitAny = await Task.WhenAny(
              ctx.inquiry.WaitForFinish,
              msg.WaitResponse(TimeSpan.FromHours(1)));
          if (waitAny is not Task<ClientMessageDto> responseTask) {
            // Inquiry finished, no need to wait for response.
            return;
          }
          var resp = responseTask.Result?.ClientMsg?.InquiryMsg;
          var inquiryResp = resp == null ? InquiryResponse.Default(seat)
              : new InquiryResponse(seat, resp.Index, resp.Response);
          if (ctx.inquiry.OnResponse(inquiryResp)) {
            EndInquiry(ctx);
          }
        }
        _ = WaitResponse();
      }
      return msg;
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