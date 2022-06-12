using RabiRiichi.Actions;
using System.Text.Json.Serialization;

namespace RabiRiichi.Server.Messages {
    public class OutInquiry {
        public SinglePlayerInquiry inquiry;

        public static OutInquiry From(SinglePlayerInquiry inquiry) {
            return new OutInquiry {
                inquiry = inquiry,
            };
        }
    }

    public class InInquiryResponse {
        public int id;

        [JsonPropertyName("idx")]
        public int index;

        [JsonPropertyName("resp")]
        public string response;
    }
}