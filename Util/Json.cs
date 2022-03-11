using System.Text.Json;

namespace RabiRiichi.Util {
    public static class Json {
        public static readonly JsonSerializerOptions options
            = new(JsonSerializerDefaults.Web) {
                IncludeFields = true,
            };

        public static string ToJson<T>(T obj) {
            return JsonSerializer.Serialize(obj, options);
        }

        public static T FromJson<T>(string json) {
            return JsonSerializer.Deserialize<T>(json, options);
        }
    }
}