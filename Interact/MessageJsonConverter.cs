using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Interact {
    public class MessageJsonConverter : JsonConverterFactory {
        public readonly int playerId;

        public MessageJsonConverter(int playerId) {
            this.playerId = playerId;
        }

        public override bool CanConvert(Type typeToConvert) {
            return typeToConvert.GetCustomAttribute<RabiMessageAttribute>() != null;
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
            return (JsonConverter)Activator.CreateInstance(
                typeof(RabiMessageConverter<>).MakeGenericType(typeToConvert),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: new object[] { playerId },
                culture: null
            );
        }

        private class RabiMessageConverter<T> : JsonConverter<T> {
            #region static
            private static readonly MemberInfo[] AllMembers;
            private static readonly MemberInfo[] BroadCastMembers;

            static RabiMessageConverter() {
                var type = typeof(T);
                if (type.GetCustomAttribute<RabiMessageAttribute>() == null) {
                    throw new ArgumentException($"{type} is not a RabiMessage");
                }
                var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var AllProperties = type.GetProperties(bindingFlags)
                    .Where(p => p.GetCustomAttribute<RabiPrivateAttribute>() != null
                        || p.GetCustomAttribute<RabiBroadcastAttribute>() != null);
                var AllFields = type.GetFields(bindingFlags)
                    .Where(f => f.GetCustomAttribute<RabiPrivateAttribute>() != null
                        || f.GetCustomAttribute<RabiBroadcastAttribute>() != null);

                if (AllProperties.Any(p => p.GetCustomAttribute<JsonIncludeAttribute>() == null && p.GetSetMethod() == null)) {
                    throw new ArgumentException($"{type} has properties with private setter. Consider using [JsonInclude].");
                }
                if (AllFields.Any(f => f.GetCustomAttribute<JsonIncludeAttribute>() == null)) {
                    throw new ArgumentException($"{type} has fields with private setter. Consider using [JsonInclude].");
                }

                var BroadCastProperties = AllProperties.Where(p => p.GetCustomAttribute<RabiBroadcastAttribute>() != null);
                var BroadCastFields = AllFields.Where(f => f.GetCustomAttribute<RabiBroadcastAttribute>() != null);
                AllMembers = AllProperties.Cast<MemberInfo>().Concat(AllFields).ToArray();
                BroadCastMembers = BroadCastProperties.Cast<MemberInfo>().Concat(BroadCastFields).ToArray();

                if (!type.IsAssignableTo(typeof(IWithPlayer))) {
                    if (AllMembers.Length != BroadCastMembers.Length) {
                        throw new ArgumentException($"{type} is not IWithPlayer but has properties or fields that are not broadcastable.");
                    }
                }
            }

            private static object GetValue(MemberInfo info, object obj) {
                return info.MemberType switch {
                    MemberTypes.Property => ((PropertyInfo)info).GetValue(obj),
                    MemberTypes.Field => ((FieldInfo)info).GetValue(obj),
                    _ => throw new ArgumentException($"{info} is not a property or field."),
                };
            }
            #endregion

            private readonly int playerId;

            public RabiMessageConverter(int playerId) {
                this.playerId = playerId;
            }

            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
                options.IncludeFields = true;
                return (T)JsonSerializer.Deserialize(ref reader, typeToConvert, options);
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) {
                options.IncludeFields = true;
                // Check which fields and properties to include
                var members = (value is IWithPlayer iwp && iwp.player.id != playerId) ? BroadCastMembers : AllMembers;

                // Write stringified json
                writer.WriteStartObject();
                foreach (var member in members) {
                    var name = member.Name;
                    if (options.PropertyNamingPolicy != null) {
                        name = options.PropertyNamingPolicy.ConvertName(name);
                    }
                    writer.WritePropertyName(name);
                    JsonSerializer.Serialize(writer, GetValue(member, value), options);
                }
                writer.WriteEndObject();
            }
        }
    }
}