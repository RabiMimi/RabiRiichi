using System;
using System.Reflection;


namespace RabiRiichi.Communication {
    public static class RabiAttributeExtensions {
        public static bool IsRabiPrivate(this Type type)
            => type.GetCustomAttribute<RabiPrivateAttribute>() != null;

        public static bool IsRabiIgnore(this Type type)
            => type.GetCustomAttribute<RabiIgnoreAttribute>() != null;

        public static bool IsRabiPrivate(this IRabiMessage msg)
            => msg.GetType().IsRabiPrivate();

        public static bool IsRabiIgnore(this IRabiMessage msg)
            => msg.GetType().IsRabiIgnore();

        public static bool IsRabiPrivate(this FieldInfo field)
            => field.GetCustomAttribute<RabiPrivateAttribute>() != null;

        public static bool IsRabiBroadcast(this FieldInfo field)
            => field.GetCustomAttribute<RabiBroadcastAttribute>() != null;

        public static bool IsRabiPrivate(this PropertyInfo property)
            => property.GetCustomAttribute<RabiPrivateAttribute>() != null;

        public static bool IsRabiBroadcast(this PropertyInfo property)
            => property.GetCustomAttribute<RabiBroadcastAttribute>() != null;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
    public class RabiPrivateAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
    public class RabiBroadcastAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class RabiIgnoreAttribute : Attribute { }
}
