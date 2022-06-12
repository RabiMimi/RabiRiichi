using System.Text.Json.Serialization;

namespace RabiRiichi.Server.Messages {
    public class InInquiryResponse {
        public int id;

        [JsonPropertyName("idx")]
        public int index;

        [JsonPropertyName("resp")]
        public string response;
    }
}