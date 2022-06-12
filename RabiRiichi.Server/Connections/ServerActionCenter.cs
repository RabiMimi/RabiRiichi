using RabiRiichi.Actions;
using RabiRiichi.Communication;
using RabiRiichi.Events;
using RabiRiichi.Server.Messages;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Connections {
    public class ServerActionCenter : IActionCenter {
        private class InquiryContext {
            public readonly MultiPlayerInquiry inquiry;

            /// <summary> (msgId, playerId) </summary>
            public readonly List<(int, int)> messages = new();

            public InquiryContext(MultiPlayerInquiry inquiry) {
                this.inquiry = inquiry;
            }

            public bool OnResponse(int playerId, InInquiryResponse resp) {
                var item = messages.Find(x => x.Item1 == resp.id && x.Item2 == playerId);
                if (item == default) {
                    return false;
                }
                return inquiry.OnResponse(new InquiryResponse(playerId, resp.index, resp.response));
            }
        }

        private readonly Room room;
        private InquiryContext context;

        public ServerActionCenter(Room room) {
            this.room = room;
            for (int i = 0; i < room.players.Length; i++) {
                room.players[i].connection.OnReceive +=
                    (InMessage msg) => OnReceiveMessage(i, msg);
            }
        }

        private int SendMessage(int playerId, string type, object msg)
            => room.players[playerId].connection.Queue(type, msg);

        private void OnReceiveMessage(int playerId, InMessage msg) {
            if (!msg.TryGetMessage<InInquiryResponse>(out var resp)) {
                return;
            }
            var ctx = context;
            if (ctx?.OnResponse(playerId, resp) == true) {
                EndInquiry(ctx);
            }
        }

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
            if (Interlocked.CompareExchange(ref this.context, ctx, null) != null) {
                throw new InvalidOperationException("Inquiry already in progress");
            }
            foreach (var playerInquiry in inquiry.playerInquiries) {
                int playerId = playerInquiry.playerId;
                int msgId = SendMessage(playerId, OutMsgType.Inquiry, OutInquiry.From(playerInquiry));
                ctx.messages.Add((msgId, playerId));
            }
        }

        public void OnMessage(int playerId, object msg) {
            SendMessage(playerId, OutMsgType.Other, msg);
        }
    }
}