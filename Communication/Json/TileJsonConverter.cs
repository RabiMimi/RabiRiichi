using RabiRiichi.Core;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Communication.Json {
    public abstract class ToStringJsonConverter<T> : JsonConverter<T> {
        public abstract T FromString(string str);

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            return FromString(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) {
            writer.WriteStringValue(value.ToString());
        }
    }

    public class TileJsonConverter : ToStringJsonConverter<Tile> {
        public override Tile FromString(string str) {
            return new Tile(str);
        }
    }

    public class TilesJsonConverter : ToStringJsonConverter<Tiles> {
        public override Tiles FromString(string str) {
            return new Tiles(str);
        }
    }
}