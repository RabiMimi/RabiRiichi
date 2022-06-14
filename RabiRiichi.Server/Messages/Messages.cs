using RabiRiichi.Util;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Server.Messages {
    /// <summary>
    /// Messages to send to client.
    /// </summary>
    public class OutMessage {
        public int id;

        [JsonPropertyName("resp")]
        public int? respondTo;
        public string type;

        [JsonPropertyName("msg")]
        public object message;

        [JsonIgnore] public readonly AtomicBool isQueued = new();
        [JsonIgnore] public readonly TaskCompletionSource<InMessage> responseTcs = new();

        public OutMessage(int id, string type, object message, int? respondTo = null) {
            this.id = id;
            this.type = type;
            this.message = message;
            this.respondTo = respondTo;
        }
    }

    /// <summary>
    /// Messages received from client.
    /// </summary>
    public class InMessage {
        public static readonly JsonSerializerOptions jsonSerializerOptions = new() {
            IncludeFields = true,
        };

        public int id;

        [JsonPropertyName("resp")]
        public int respondTo;
        public string type;

        [JsonPropertyName("msg")]
        public JsonElement message;

        [JsonIgnore]
        private readonly Lazy<object> lazyMessage;

        public InMessage() {
            lazyMessage = new(() => {
                var msgType = type switch {
                    InMsgType.HeartBeat => typeof(InOutHeartBeat),
                    InMsgType.InquiryResponse => typeof(InInquiryResponse),
                    InMsgType.RoomUpdate => typeof(InRoomUpdate),
                    InMsgType.VersionCheck => typeof(InVersionCheck),
                    _ => null,
                };
                if (msgType == null) {
                    return null;
                }
                try {
                    return JsonSerializer.Deserialize(message, msgType, jsonSerializerOptions);
                } catch (JsonException) {
                    return null;
                }
            });
        }

        public bool TryGetMessage<T>(out T message) {
            if (lazyMessage.Value is T msg) {
                message = msg;
                return true;
            }
            message = default;
            return false;
        }
    }
}