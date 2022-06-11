using System.Text.Json.Serialization;

namespace RabiRiichi.Server.Messages {
    /// <summary>
    /// HeartBeat message. Should always have id = -1.
    /// </summary>
    public class InOutHeartBeat {
        [JsonPropertyName("id")]
        public int maxMsgId { get; init; }

        [JsonPropertyName("reqs")]
        public List<int> requestingEvents;

        public static InOutHeartBeat From(int maxMsgId, List<int> requestingEvents) {
            return new InOutHeartBeat {
                maxMsgId = maxMsgId,
                requestingEvents = requestingEvents,
            };
        }
    }
}