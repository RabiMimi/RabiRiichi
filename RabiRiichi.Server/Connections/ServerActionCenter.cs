using RabiRiichi.Actions;
using RabiRiichi.Communication;
using RabiRiichi.Communication.Proto;
using RabiRiichi.Events;
using RabiRiichi.Generated.Actions;
using RabiRiichi.Generated.Events;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Connections {
    public class ServerActionCenter : IActionCenter {
        private class InquiryContext {
            public readonly MultiPlayerInquiry inquiry;
            public readonly int[] inquiryIds;

            public InquiryContext(MultiPlayerInquiry inquiry) {
                this.inquiry = inquiry;
                this.inquiryIds = new int[inquiry.playerInquiries.Count];
            }
        }

        private readonly Room room;
        private InquiryContext context;

        public ServerActionCenter(Room room) {
            this.room = room;
        }

        private ServerMessageWrapper SendMessage(int seat, ServerMessageDto msg)
            => room.GetPlayerBySeat(seat).connection.Queue(msg);

        private void EndInquiry(InquiryContext context) {
            var oldContext = Interlocked.CompareExchange(ref this.context, null, context);
            if (oldContext != context) {
                return;
            }
            oldContext.inquiry.Finish();
            for (int i = 0; i < oldContext.inquiry.playerInquiries.Count; i++) {
                SendMessage(
                    oldContext.inquiry.playerInquiries[i].playerId,
                    ProtoUtils.CreateDto(new ServerInquiryEndMsg {
                        EndId = oldContext.inquiryIds[i],
                    }));
            }
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
            var inquiryMsg = new ServerInquiryMsg {
                Inquiry = ctx.inquiry.game.SerializeProto<SinglePlayerInquiryMsg>(inquiry, seat)
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
            if (Interlocked.CompareExchange(ref context, ctx, null) != null) {
                throw new InvalidOperationException("Inquiry already in progress");
            }
            for (int i = 0; i < inquiry.playerInquiries.Count; i++) {
                var playerInquiry = inquiry.playerInquiries[i];
                var msg = SendInquiry(ctx, playerInquiry.playerId);
                ctx.inquiryIds[i] = msg.msg.Id;
            }
            if (inquiry.IsEmpty) {
                EndInquiry(ctx);
            }
        }
    }
}