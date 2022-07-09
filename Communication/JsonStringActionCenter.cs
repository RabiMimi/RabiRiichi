using RabiRiichi.Actions;
using RabiRiichi.Communication.Json;
using RabiRiichi.Events;
using System.Threading;

namespace RabiRiichi.Communication {
    public class JsonStringActionCenter : IActionCenter {
        public delegate void MessageSender(int playerId, string message);

        private readonly MessageSender sender;
        private MultiPlayerInquiry current;

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

        public void OnMessage(int playerId, int actionIndex, string message) {
            var inq = current;
            if (inq?.OnResponse(new InquiryResponse(playerId, actionIndex, message)) == true) {
                Interlocked.CompareExchange(ref current, null, inq);
            }
        }

        public void OnInquiry(MultiPlayerInquiry inquiry) {
            if (inquiry.IsEmpty) {
                return;
            }
            current = inquiry;
            foreach (var singlePlayerInquiry in inquiry.playerInquiries) {
                var json = RabiJson.Stringify(singlePlayerInquiry, singlePlayerInquiry.playerId);
                Send(singlePlayerInquiry.playerId, json);
            }
        }

        public void OnEvent(int playerId, EventBase ev) {
            var json = RabiJson.Stringify(ev, playerId);
            Send(playerId, json);
        }
    }
}