using System;
using System.Collections.Generic;
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

        private class RabiMessageReflectionData {
            private static readonly Dictionary<Type, RabiMessageReflectionData> reflectionDataDict = new();
            public static RabiMessageReflectionData Of(Type type) {
                if (!reflectionDataDict.TryGetValue(type, out var reflectionData)) {
                    reflectionData = new RabiMessageReflectionData(type);
                    reflectionDataDict.Add(type, reflectionData);
                }
                return reflectionData;
            }

            public readonly MemberInfo[] AllMembers;
            public readonly MemberInfo[] BroadCastMembers;
            public readonly bool isPrivate;

            public RabiMessageReflectionData(Type type) {
                if (type.GetCustomAttribute<RabiMessageAttribute>() == null) {
                    throw new ArgumentException($"{type} is not a RabiMessage");
                }
                isPrivate = type.GetCustomAttribute<RabiPrivateAttribute>() != null;
                var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var AllProperties = type.GetProperties(bindingFlags)
                    .Where(p => p.GetCustomAttribute<RabiPrivateAttribute>() != null
                        || p.GetCustomAttribute<RabiBroadcastAttribute>() != null);
                var AllFields = type.GetFields(bindingFlags)
                    .Where(f => f.GetCustomAttribute<RabiPrivateAttribute>() != null
                        || f.GetCustomAttribute<RabiBroadcastAttribute>() != null);

                var BroadCastProperties = AllProperties.Where(p => p.GetCustomAttribute<RabiBroadcastAttribute>() != null);
                var BroadCastFields = AllFields.Where(f => f.GetCustomAttribute<RabiBroadcastAttribute>() != null);
                AllMembers = AllProperties.Cast<MemberInfo>().Concat(AllFields).ToArray();
                BroadCastMembers = BroadCastProperties.Cast<MemberInfo>().Concat(BroadCastFields).ToArray();

                if (!type.IsAssignableTo(typeof(IRabiPlayerMessage))) {
                    if (AllMembers.Length != BroadCastMembers.Length) {
                        throw new JsonException($"{type} is not IWithPlayer but has properties or fields that are not broadcastable.");
                    }
                    if (isPrivate) {
                        throw new JsonException($"{type} is not IWithPlayer but marked as RabiPrivate.");
                    }
                }
            }
        }

        private class RabiMessageConverter<T> : JsonConverter<T> {
            private static object GetValue(MemberInfo info, object obj) {
                return info.MemberType switch {
                    MemberTypes.Property => ((PropertyInfo)info).GetValue(obj),
                    MemberTypes.Field => ((FieldInfo)info).GetValue(obj),
                    _ => throw new JsonException($"{info} is not a property or field."),
                };
            }

            private readonly int playerId;

            public RabiMessageConverter(int playerId) {
                this.playerId = playerId;
            }

            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
                var newOptions = new JsonSerializerOptions(options);
                newOptions.Converters.Clear();
                return JsonSerializer.Deserialize<T>(ref reader, newOptions);
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) {
                var reflectionData = RabiMessageReflectionData.Of(value.GetType());
                // Check if entire class is private
                if (reflectionData.isPrivate) {
                    if (((IRabiPlayerMessage)value).playerId != this.playerId) {
                        writer.WriteNullValue();
                        return;
                    }
                }

                // Check which fields and properties to include
                var members = (value is IRabiPlayerMessage iwp && iwp.playerId != playerId) ? reflectionData.BroadCastMembers : reflectionData.AllMembers;

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