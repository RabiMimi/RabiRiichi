using RabiRiichi.Actions;
using RabiRiichi.Communication;
using RabiRiichi.Events;
using RabiRiichi.Server.Messages;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Utils {
    public class ServerActionCenter : IActionCenter {
        private class InquiryContext {
            public readonly MultiPlayerInquiry inquiry;

            public InquiryContext(MultiPlayerInquiry inquiry) {
                this.inquiry = inquiry;
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
        }

        public void OnEvent(int playerId, EventBase ev) {
            SendMessage(playerId, OutMsgType.Event, ev);
        }

        public void OnInquiry(MultiPlayerInquiry inquiry) {
            var ctx = new InquiryContext(inquiry);
            if (Interlocked.CompareExchange(ref context, ctx, null) != null) {
                throw new InvalidOperationException("Inquiry already in progress");
            }
            foreach (var playerInquiry in inquiry.playerInquiries) {
                int playerId = playerInquiry.playerId;
                var msg = SendMessage(playerId, OutMsgType.Inquiry, OutInquiry.From(playerInquiry));
                if (msg != null) {
                    Task.Run(async () => {
                        var resp = await msg.WaitResponse<InInquiryResponse>(TimeSpan.FromHours(1));
                        var inquiryResp = resp == null ? InquiryResponse.Default(playerId)
                            : new InquiryResponse(playerId, resp.index, resp.response);
                        if (inquiry.OnResponse(inquiryResp)) {
                            EndInquiry(ctx);
                        }
                    });
                }
            }
        }

        public void OnMessage(int playerId, object msg) {
            SendMessage(playerId, OutMsgType.Other, msg);
        }
    }
}