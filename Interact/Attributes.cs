using System;

namespace RabiRiichi.Interact {
    [AttributeUsage(AttributeTargets.Class)]
    public class RabiMessageAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
    public class RabiPrivateAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
    public class RabiBroadcastAttribute : Attribute { }
}