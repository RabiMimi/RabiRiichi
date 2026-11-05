using System;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Communication.Json {
  /// <summary>Decimal strings preserve scores in JavaScript and persisted results.</summary>
  public sealed class BigIntegerJsonConverter : JsonConverter<BigInteger> {
    public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
      if (reader.TokenType == JsonTokenType.String) {
        return BigInteger.Parse(reader.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture);
      }
      // Existing match records and config JSON used numeric point values.
      using var document = JsonDocument.ParseValue(ref reader);
      return BigInteger.Parse(document.RootElement.GetRawText(), NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
  }
}
