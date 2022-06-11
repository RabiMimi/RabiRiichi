using RabiRiichi.Util;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Server.Messages {
    /// <summary>
    /// Messages to send to client.
    /// </summary>
    public class OutMessage {
        public int id;
        public string type;
        public object message;

        [JsonIgnore] public readonly AtomicBool isQueued = new();

        public OutMessage(int id, string type, object message) {
            this.id = id;
            this.type = type;
            this.message = message;
        }
    }

    /// <summary>
    /// Messages received from client.
    /// </summary>
    public class InMessage {
        public int id;
        public string type;
        public JsonElement message;

        [JsonIgnore]
        private Lazy<object> messageObj = new(() => {

        });

        public bool TryGetMessage<T>(out T message) {
            if (messageObj is T msg) {
                message = msg;
                return true;
            }
            message = default;
            return false;
        }
    }
}