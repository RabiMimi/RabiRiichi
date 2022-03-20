using RabiRiichi.Action;
using RabiRiichi.Event;
using System.Collections.Generic;

namespace RabiRiichi.Communication {
    public class JsonStringActionCenter : IActionCenter {
        public delegate void MessageSender(int playerId, string message);

        private readonly MessageSender sender;
        private readonly Dictionary<int, MultiPlayerInquiry> inquiries = new();

        public JsonStringActionCenter(MessageSender sender) {
            this.sender = sender;
        }

        public void OnMessage(int inquiryId, int playerId, int actionIndex, string message) {
            if (!inquiries.TryGetValue(inquiryId, out var inquiry)) {
                return;
            }
            inquiry.OnResponse(new InquiryResponse(playerId, actionIndex, message));
        }

        public void OnInquiry(MultiPlayerInquiry inquiry) {
            inquiries[inquiry.id] = inquiry;
            foreach (var singlePlayerInquiry in inquiry.playerInquiries) {
                sender(singlePlayerInquiry.playerId,
                    inquiry.game.json.Stringify(singlePlayerInquiry, singlePlayerInquiry.playerId));
            }
        }

        public void OnEvent(int playerId, EventBase ev) {
            var json = ev.game.json.Stringify(ev, playerId);
            sender(playerId, json);
        }
    }
}