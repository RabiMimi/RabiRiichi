using System.Text.Json.Serialization;

namespace RabiRiichi.Server.Messages {
    /// <summary>
    /// HeartBeat message. Should always have id = -1.
    /// </summary>
    public class HeartBeatMessage {
        [JsonPropertyName("id")]
        public int maxEventId { get; init; }

        public HeartBeatMessage(int maxEventId) {
            this.maxEventId = maxEventId;
        }
    }
}