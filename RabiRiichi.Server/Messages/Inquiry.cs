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

    public class OutFinishInquiry {
        public int id;

        public static OutFinishInquiry From(int id) {
            return new OutFinishInquiry {
                id = id,
            };
        }
    }

    public class InInquiryResponse {
        [JsonPropertyName("idx")]
        public int index;

        [JsonPropertyName("resp")]
        public string response;
    }
}