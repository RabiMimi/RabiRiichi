using System.Text.Json.Serialization;

namespace RabiRiichi.Server.Messages {
    /// <summary>
    /// HeartBeat message. Should always have id = -1.
    /// </summary>
    public class InOutHeartBeat {
        [JsonPropertyName("ev")]
        public int maxEventId { get; init; }

        [JsonPropertyName("reqs")]
        public List<int> requestingEvents;

        public static InOutHeartBeat From(int maxEventId, List<int> requestingEvents = null) {
            return new InOutHeartBeat {
                maxEventId = maxEventId,
                requestingEvents = requestingEvents,
            };
        }
    }
}