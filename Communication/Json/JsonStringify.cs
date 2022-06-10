using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Communication.Json {
    public static class JsonStringify {
        private static readonly TileJsonConverter tileJsonConverter = new();
        private static readonly TilesJsonConverter tilesJsonConverter = new();
        private static readonly JsonStringEnumConverter stringEnumConverter
            = new(new EnumNamingPolicy());
        private static readonly ConcurrentDictionary<int, JsonSerializerOptions> optionsDict = new();

        private static JsonSerializerOptions GetOption(int playerId) {
            return optionsDict.GetOrAdd(playerId, _ => new JsonSerializerOptions {
                Converters = {
                    new MessageJsonConverter(playerId),
                    tileJsonConverter,
                    tilesJsonConverter,
                    stringEnumConverter
                },
                IncludeFields = true,
            });
        }

        public static string Stringify(object obj, int playerId) {
            return JsonSerializer.Serialize(obj, GetOption(playerId));
        }

        public static T Parse<T>(string json, int playerId) {
            return JsonSerializer.Deserialize<T>(json, GetOption(playerId));
        }

        public static object Parse(string json, Type type, int playerId) {
            return JsonSerializer.Deserialize(json, type, GetOption(playerId));
        }
    }
}