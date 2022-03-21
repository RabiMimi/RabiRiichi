using RabiRiichi.Riichi;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Communication {
    public class JsonStringify {
        private readonly JsonSerializerOptions[] options;

        public JsonStringify(GameConfig config) {
            var tileConverter = new TileJsonConverter();
            var tilesConverter = new TilesJsonConverter();
            var stringEnumConverter = new JsonStringEnumConverter(new EnumNamingPolicy());
            options = new JsonSerializerOptions[config.playerCount];
            for (int i = 0; i < config.playerCount; i++) {
                options[i] = new JsonSerializerOptions {
                    Converters = {
                        new MessageJsonConverter(i),
                        tileConverter,
                        tilesConverter,
                        stringEnumConverter
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