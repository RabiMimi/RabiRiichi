using RabiRiichi.Action;
using RabiRiichi.Communication.Json;
using RabiRiichi.Core;
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

        private void Send(int playerId, string json) {
            if (string.IsNullOrWhiteSpace(json) || !json.StartsWith('{')) {
                // Ignore null objects
                return;
            }
            sender?.Invoke(playerId, json);
        }

        public void OnMessage(int inquiryId, int playerId, int actionIndex, string message) {
            lock (inquiries) {
                if (!inquiries.TryGetValue(inquiryId, out var inquiry)) {
                    return;
                }
                if (inquiry.OnResponse(new InquiryResponse(playerId, actionIndex, message))) {
                    inquiries.Remove(inquiryId);
                }
            }
        }

        public void OnInquiry(MultiPlayerInquiry inquiry) {
            if (inquiry.IsEmpty) {
                return;
            }
            lock (inquiries) {
                inquiries[inquiry.id] = inquiry;
            }
            foreach (var singlePlayerInquiry in inquiry.playerInquiries) {
                var json = RabiJson.Stringify(singlePlayerInquiry, singlePlayerInquiry.playerId);
                Send(singlePlayerInquiry.playerId, json);
            }
        }

        public void OnEvent(int playerId, EventBase ev) {
            var json = RabiJson.Stringify(ev, playerId);
            Send(playerId, json);
        }

        public void OnMessage(Game game, int playerId, IRabiMessage msg) {
            var json = RabiJson.Stringify(msg, playerId);
            Send(playerId, json);
        }
    }
}