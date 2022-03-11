using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Interact {
    public class MessageJsonConverter : JsonConverterFactory {
        public override bool CanConvert(Type typeToConvert) {
            return typeToConvert.GetCustomAttribute<RabiMessage>() != null;
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
            throw new NotImplementedException();
        }
    }
}