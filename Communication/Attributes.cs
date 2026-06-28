using System;
using System.Reflection;


namespace RabiRiichi.Communication {
  public static class AttributeExtensions {
    public static bool Has<T>(this Type type) where T : Attribute {
      return type.GetCustomAttribute<T>() != null;
    }

    public static bool Has<T>(this FieldInfo field) where T : Attribute {
      return field.GetCustomAttribute<T>() != null;
    }

    public static bool Has<T>(this PropertyInfo property) where T : Attribute {
      return property.GetCustomAttribute<T>() != null;
    }
  }

  [AttributeUsage(AttributeTargets.Class)]
  public class RabiMessageAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
  public class RabiPrivateAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
  public class RabiBroadcastAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Class)]
  public class RabiIgnoreAttribute : Attribute { }
}