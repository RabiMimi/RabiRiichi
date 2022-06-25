using RabiRiichi.Actions;
using RabiRiichi.Communication;
using RabiRiichi.Events;
using RabiRiichi.Server.Messages;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Utils {
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

        private OutMessage SendMessage(int playerId, string type, object msg)
            => room.players[playerId].connection.Queue(type, msg);

        private void EndInquiry(InquiryContext context) {
            var oldContext = Interlocked.CompareExchange(ref this.context, null, context);
            if (oldContext != context) {
                return;
            }
            oldContext.inquiry.Finish();
            for (int i = 0; i < oldContext.inquiry.playerInquiries.Count; i++) {
                SendMessage(
                    oldContext.inquiry.playerInquiries[i].playerId,
                    OutMsgType.FinishInquiry,
                    OutFinishInquiry.From(oldContext.inquiryIds[i]));
            }
        }

        public void OnEvent(int playerId, EventBase ev) {
            SendMessage(playerId, OutMsgType.GameEvent, ev);
        }

        private OutMessage SendInquiry(InquiryContext ctx, int playerId) {
            var inquiry = ctx?.inquiry.GetByPlayerId(playerId);
            if (inquiry == null) {
                return null;
            }
            var msg = SendMessage(playerId, OutMsgType.Inquiry, OutInquiry.From(inquiry));
            if (msg != null) {
                async Task WaitResponse() {
                    var waitAny = await Task.WhenAny(
                        ctx.inquiry.WaitForFinish,
                        msg.WaitResponse<InInquiryResponse>(TimeSpan.FromHours(1)));
                    if (waitAny is not Task<InInquiryResponse> responseTask) {
                        // Inquiry finished, no need to wait for response.
                        return;
                    }
                    var resp = responseTask.Result;
                    var inquiryResp = resp == null ? InquiryResponse.Default(playerId)
                        : new InquiryResponse(playerId, resp.index, resp.response);
                    if (ctx.inquiry.OnResponse(inquiryResp)) {
                        EndInquiry(ctx);
                    }
                }
                _ = WaitResponse();
            }
            return msg;
        }

        public void SyncInquiryTo(int playerId) {
            SendInquiry(context, playerId);
        }

        public void OnInquiry(MultiPlayerInquiry inquiry) {
            var ctx = new InquiryContext(inquiry);
            if (Interlocked.CompareExchange(ref context, ctx, null) != null) {
                throw new InvalidOperationException("Inquiry already in progress");
            }
            for (int i = 0; i < inquiry.playerInquiries.Count; i++) {
                var playerInquiry = inquiry.playerInquiries[i];
                var msg = SendInquiry(ctx, playerInquiry.playerId);
                if (msg != null) {
                    ctx.inquiryIds[i] = msg.id;
                }
            }
            if (inquiry.IsEmpty) {
                EndInquiry(ctx);
            }
        }
    }
}