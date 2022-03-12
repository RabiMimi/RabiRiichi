using RabiRiichi.Riichi;
using System;
using System.Text.Json;


namespace RabiRiichi.Interact {
    public class JsonStringify {
        private readonly JsonSerializerOptions[] options;

        public JsonStringify(GameConfig config) {
            options = new JsonSerializerOptions[config.playerCount];
            for (int i = 0; i < config.playerCount; i++) {
                options[i] = new JsonSerializerOptions {
                    Converters = {
                        new MessageJsonConverter(i)
                    },
                    IncludeFields = true,
                };
            }
        }

        public string Stringify(object obj, int playerId) {
            return JsonSerializer.Serialize(obj, options[playerId]);
        }

        public T Parse<T>(string json, int playerId) {
            return JsonSerializer.Deserialize<T>(json, options[playerId]);
        }

        public object Parse(string json, Type type, int playerId) {
            return JsonSerializer.Deserialize(json, type, options[playerId]);
        }
    }
}